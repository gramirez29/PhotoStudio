import { FINAL_BATCH_SIZE, FINAL_UPLOAD_CONCURRENCY } from '../constants/finals';
import type { FinalUploadItem, FinalUploader, FinalUploaderDeps } from '../types/media/finalUploader.types';
import { mapWithLimit } from '../utils/concurrency';
import { describeFinalsRequestError, describeRejection, describeUploadFailure } from '../utils/proofMessages';

/**
 * Creates the uploader of final photos. Unlike the proofs, a final is sent as it is: nothing is rendered, because the file the
 * photographer edited is the product.
 * @param deps What the uploader needs from outside.
 * @returns The uploader.
 */
export function createFinalUploader(deps: FinalUploaderDeps): FinalUploader {
  return {
    async run(galleryId, items, observer, signal) {
      for (let start = 0; start < items.length; start += FINAL_BATCH_SIZE) {
        if (isCancelled(signal)) {
          return { stopped: true, problem: null };
        }

        const batch = items.slice(start, start + FINAL_BATCH_SIZE);
        let sent: FinalUploadItem[];
        try {
          const slots = await deps.api.requestFinalUploads(
            galleryId,
            batch.map((item) => ({ photoId: item.photoId, bytes: item.file.size })),
            signal,
          );
          const results = await mapWithLimit(batch, FINAL_UPLOAD_CONCURRENCY, async (item, index) => {
            const slot = slots[index];
            if (slot === undefined) {
              observer.onItem(item.id, 'failed', describeUploadFailure(null));
              return null;
            }

            observer.onItem(item.id, 'uploading', null);
            try {
              await deps.put(slot, item.file, signal);
              return item;
            } catch (error) {
              if (isCancelled(signal)) {
                throw error;
              }

              observer.onItem(item.id, 'failed', describeUploadFailure(error));
              return null;
            }
          });
          sent = results.filter((item): item is FinalUploadItem => item !== null);
        } catch (error) {
          if (isCancelled(signal)) {
            return { stopped: true, problem: null };
          }

          const problem = describeFinalsRequestError(error);
          for (const item of batch) {
            observer.onItem(item.id, 'failed', problem);
          }

          return { stopped: true, problem };
        }

        if (sent.length === 0) {
          continue;
        }

        for (const item of sent) {
          observer.onItem(item.id, 'registering', null);
        }

        try {
          const details = await Promise.all(
            sent.map(async (item) => {
              const info = await deps.readInfo(item.file);
              return { photoId: item.photoId, width: info.width, height: info.height, capturedAt: info.capturedAt };
            }),
          );
          const result = await deps.api.registerFinals(galleryId, sent.map((item) => item.photoId), details, signal);
          const rejected = new Map(result.rejected.map((rejection) => [rejection.photoId, rejection.reason]));
          for (const item of sent) {
            const reason = rejected.get(item.photoId);
            if (reason === undefined) {
              observer.onDone(item.id);
            } else {
              observer.onItem(item.id, 'failed', describeRejection(reason));
            }
          }

          observer.onRegistered(result.gallery);
        } catch (error) {
          if (isCancelled(signal)) {
            return { stopped: true, problem: null };
          }

          const problem = describeFinalsRequestError(error);
          for (const item of sent) {
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
 * Tells whether a run was cancelled. It is a function so the check is made again each time.
 * @param signal Signal of the run, if it has one.
 * @returns True when the run was cancelled.
 */
function isCancelled(signal: AbortSignal | undefined): boolean {
  return signal?.aborted === true;
}
