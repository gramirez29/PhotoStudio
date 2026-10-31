import { PROOF_BATCH_SIZE, PROOF_PREPARE_CONCURRENCY, PROOF_UPLOAD_CONCURRENCY } from '../constants/proof';
import type { RenderedProof } from '../types/media/proofRenderer.types';
import type { ProofUploadSlot } from '../types/api/proofs';
import type { ProofUploader, ProofUploaderDeps, UploadItem, UploadObserver } from '../types/media/proofUploader.types';
import { mapWithLimit } from '../utils/concurrency';
import { proofLabel } from '../utils/proofImage';
import { describeProofRequestError, describeRejection, describeRenderFailure, describeUploadFailure } from '../utils/proofMessages';

/** A photo that was turned into a proof. */
interface Prepared {
  /** The photo of the queue. */
  readonly item: UploadItem;
  /** The proof. */
  readonly proof: RenderedProof;
}

/**
 * Creates the uploader of proofs: the steps from a photo on the computer to a proof registered in the gallery. It uploads a list of
 * photos a batch at a time: each batch is prepared, sent straight to the storage and registered before the next starts, so memory
 * holds a few proofs and the registrations never run at the same time. A photo that fails does not stop the others; a request that
 * fails (the API, or no connection) stops the run and leaves the rest waiting.
 * @param deps What the uploader needs from outside.
 * @returns The uploader.
 */
export function createProofUploader(deps: ProofUploaderDeps): ProofUploader {
  /**
   * Turns every photo of a batch into a proof, a few at a time. A photo that cannot be turned is marked failed and left out.
   * @param batch The photos of the batch.
   * @param observer Told about every change.
   * @returns The photos that became proofs.
   */
  async function prepareBatch(batch: readonly UploadItem[], observer: UploadObserver): Promise<Prepared[]> {
    const results = await mapWithLimit(batch, PROOF_PREPARE_CONCURRENCY, async (item): Promise<Prepared | null> => {
      observer.onItem(item.id, 'preparing', null);
      try {
        return { item, proof: await deps.renderer.render(item.file) };
      } catch (error) {
        observer.onItem(item.id, 'failed', describeRenderFailure(error));
        return null;
      }
    });
    return results.filter((prepared): prepared is Prepared => prepared !== null);
  }

  /**
   * Sends the proofs of a batch to the storage, a few at a time. A proof that cannot be sent is marked failed and left out.
   * @param prepared The proofs of the batch.
   * @param slots The places the API gave, in the same order.
   * @param observer Told about every change.
   * @param signal Optional signal to cancel the uploads.
   * @returns The photos the storage has, each with the identifier of its place.
   */
  async function sendBatch(
    prepared: readonly Prepared[],
    slots: readonly ProofUploadSlot[],
    observer: UploadObserver,
    signal: AbortSignal | undefined,
  ): Promise<{ readonly item: UploadItem; readonly photoId: string }[]> {
    const results = await mapWithLimit(prepared, PROOF_UPLOAD_CONCURRENCY, async (entry, index) => {
      const slot = slots[index];
      if (slot === undefined) {
        observer.onItem(entry.item.id, 'failed', describeUploadFailure(null));
        return null;
      }

      observer.onItem(entry.item.id, 'uploading', null);
      try {
        await deps.put(slot, entry.proof.blob, signal);
        return { item: entry.item, photoId: slot.photoId };
      } catch (error) {
        if (isCancelled(signal)) {
          throw error;
        }

        observer.onItem(entry.item.id, 'failed', describeUploadFailure(error));
        return null;
      }
    });
    return results.filter((sent): sent is { readonly item: UploadItem; readonly photoId: string } => sent !== null);
  }

  return {
    async run(galleryId, items, observer, signal) {
      for (let start = 0; start < items.length; start += PROOF_BATCH_SIZE) {
        if (isCancelled(signal)) {
          return { stopped: true, problem: null };
        }

        const batch = items.slice(start, start + PROOF_BATCH_SIZE);
        const prepared = await prepareBatch(batch, observer);
        if (prepared.length === 0) {
          continue;
        }

        let sent: { readonly item: UploadItem; readonly photoId: string }[];
        try {
          const slots = await deps.api.requestProofUploads(
            galleryId,
            prepared.map(({ item, proof }) => ({ name: proofLabel(item.file.name), bytes: proof.blob.size })),
            signal,
          );
          sent = await sendBatch(prepared, slots, observer, signal);
        } catch (error) {
          if (isCancelled(signal)) {
            return { stopped: true, problem: null };
          }

          const problem = describeProofRequestError(error);
          for (const { item } of prepared) {
            observer.onItem(item.id, 'failed', problem);
          }

          return { stopped: true, problem };
        }

        if (sent.length === 0) {
          continue;
        }

        for (const { item } of sent) {
          observer.onItem(item.id, 'registering', null);
        }

        try {
          const result = await deps.api.registerProofs(
            galleryId,
            sent.map(({ item, photoId }) => ({ photoId, name: proofLabel(item.file.name) })),
            signal,
          );
          const rejected = new Map(result.rejected.map((rejection) => [rejection.photoId, rejection.reason]));
          for (const { item, photoId } of sent) {
            const reason = rejected.get(photoId);
            if (reason === undefined) {
              observer.onDone(item.id);
            } else {
              observer.onItem(item.id, 'failed', describeRejection(reason));
            }
          }

          observer.onRegistered(result.gallery);
          if ([...rejected.values()].includes('gallery_full')) {
            return { stopped: true, problem: describeRejection('gallery_full') };
          }
        } catch (error) {
          if (isCancelled(signal)) {
            return { stopped: true, problem: null };
          }

          const problem = describeProofRequestError(error);
          for (const { item } of sent) {
            observer.onItem(item.id, 'failed', problem);
          }

          return { stopped: true, problem };
        }
      }

      return { stopped: false, problem: null };
    },
  };
}

/**
 * Tells whether a run was cancelled. It is a function, not a comparison in place, so the check is made again each time and the
 * compiler does not assume the signal stays as it was.
 * @param signal Signal of the run, if it has one.
 * @returns True when the run was cancelled.
 */
function isCancelled(signal: AbortSignal | undefined): boolean {
  return signal?.aborted === true;
}
