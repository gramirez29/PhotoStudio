import { PROOF_BATCH_SIZE, PROOF_PREPARE_CONCURRENCY, PROOF_UPLOAD_CONCURRENCY } from '../constants/upload';
import type { ProofUploadSlot } from '../types/api/gallery';
import type { ProofUploader, ProofUploaderDeps, StagedFile, UploadItem, UploadObserver } from '../types/media/upload.types';
import { mapWithLimit } from '../utils/concurrency';
import { proofLabel } from '../utils/proofImage';
import { describeRejection, describeRenderFailure, describeUploadFailure, describeUploadRequestError } from '../utils/uploadMessages';

/** A photo that was turned into a proof and waits, as a file, to be sent. */
interface Prepared {
  /** The photo of the queue. */
  readonly item: UploadItem;
  /** The proof, kept as a file in the cache of the phone. */
  readonly staged: StagedFile;
}

/** A proof the storage has, with the identifier of its place. */
interface Sent {
  /** The photo of the queue. */
  readonly item: UploadItem;
  /** Identifier the API gave to the place. */
  readonly photoId: string;
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

/**
 * Creates the uploader of proofs: the steps from a photo on the phone to a proof registered in the gallery. It uploads a list of
 * photos a batch at a time: each batch is prepared, sent straight to the storage and registered before the next starts, so the phone
 * holds a few proofs and the registrations never run at the same time. A photo that fails does not stop the others; a request that
 * fails (the API, or no connection) stops the run and leaves the rest waiting. The proofs made for a batch are deleted from the cache
 * when the batch ends, however it ends.
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
        const proof = await deps.renderer.render(item.file.uri);
        return { item, staged: deps.staging.stage(proof.bytes) };
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
  async function sendBatch(prepared: readonly Prepared[], slots: readonly ProofUploadSlot[], observer: UploadObserver, signal: AbortSignal | undefined): Promise<Sent[]> {
    const results = await mapWithLimit(prepared, PROOF_UPLOAD_CONCURRENCY, async (entry, index): Promise<Sent | null> => {
      const slot = slots[index];
      if (slot === undefined) {
        observer.onItem(entry.item.id, 'failed', describeUploadFailure(null));
        return null;
      }

      observer.onItem(entry.item.id, 'uploading', null);
      try {
        await deps.storage.put(slot, entry.staged.uri, { onProgress: (fraction) => observer.onProgress(entry.item.id, fraction), signal });
        return { item: entry.item, photoId: slot.photoId };
      } catch (error) {
        if (isCancelled(signal)) {
          throw error;
        }

        observer.onItem(entry.item.id, 'failed', describeUploadFailure(error));
        return null;
      }
    });
    return results.filter((sent): sent is Sent => sent !== null);
  }

  /**
   * Prepares, sends and registers one batch.
   * @param galleryId Gallery the proofs go to.
   * @param batch The photos of the batch.
   * @param observer Told about every change.
   * @param signal Optional signal to cancel the run.
   * @returns How the batch ended: the problem that stops the run, a cancellation, or nothing when the next batch can go on.
   */
  async function runBatch(galleryId: string, batch: readonly UploadItem[], observer: UploadObserver, signal: AbortSignal | undefined): Promise<{ readonly stop: boolean; readonly problem: string | null }> {
    const prepared = await prepareBatch(batch, observer);
    try {
      if (prepared.length === 0) {
        return { stop: false, problem: null };
      }

      let sent: Sent[];
      try {
        const slots = await deps.api.requestProofUploads(
          galleryId,
          prepared.map(({ item, staged }) => ({ name: proofLabel(item.file.name), bytes: staged.bytes })),
          signal,
        );
        sent = await sendBatch(prepared, slots, observer, signal);
      } catch (error) {
        if (isCancelled(signal)) {
          return { stop: true, problem: null };
        }

        const problem = describeUploadRequestError(error);
        for (const { item } of prepared) {
          observer.onItem(item.id, 'failed', problem);
        }

        return { stop: true, problem };
      }

      if (sent.length === 0) {
        return { stop: false, problem: null };
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
        return [...rejected.values()].includes('gallery_full') ? { stop: true, problem: describeRejection('gallery_full') } : { stop: false, problem: null };
      } catch (error) {
        if (isCancelled(signal)) {
          return { stop: true, problem: null };
        }

        const problem = describeUploadRequestError(error);
        for (const { item } of sent) {
          observer.onItem(item.id, 'failed', problem);
        }

        return { stop: true, problem };
      }
    } finally {
      for (const { staged } of prepared) {
        deps.staging.discard(staged.uri);
      }
    }
  }

  return {
    async run(galleryId, items, observer, signal) {
      for (let start = 0; start < items.length; start += PROOF_BATCH_SIZE) {
        if (isCancelled(signal)) {
          return { stopped: true, problem: null };
        }

        const outcome = await runBatch(galleryId, items.slice(start, start + PROOF_BATCH_SIZE), observer, signal);
        if (outcome.stop) {
          return { stopped: true, problem: outcome.problem };
        }
      }

      return { stopped: false, problem: null };
    },
  };
}
