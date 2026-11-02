import { FINAL_BATCH_SIZE, FINAL_UPLOAD_CONCURRENCY } from '../constants/upload';
import type { FinalUploadSlot } from '../types/api/gallery';
import type { FinalUploader, FinalUploaderDeps, FinalUploadItem, UploadObserver } from '../types/media/upload.types';
import { mapWithLimit } from '../utils/concurrency';
import { describeRejection, describeUploadFailure, describeUploadRequestError } from '../utils/uploadMessages';

/**
 * Tells whether a run was cancelled. It is a function so the check is made again each time.
 * @param signal Signal of the run, if it has one.
 * @returns True when the run was cancelled.
 */
function isCancelled(signal: AbortSignal | undefined): boolean {
  return signal?.aborted === true;
}

/**
 * Creates the uploader of final photos. Unlike the proofs, a final is sent as it is: nothing is rendered, because the file the
 * photographer edited is the product. Only its thumbnail is made, to look at in a grid.
 * @param deps What the uploader needs from outside.
 * @returns The uploader.
 */
export function createFinalUploader(deps: FinalUploaderDeps): FinalUploader {
  /**
   * Sends the thumbnail of a final. It is optional: whatever goes wrong here leaves the final without one, and the final is not
   * refused for it. Only a cancellation is passed on. The thumbnail is deleted from the cache whatever happens.
   * @param slot Where the final and its thumbnail go.
   * @param item The final.
   * @param signal Optional signal to cancel the run.
   */
  async function sendThumb(slot: FinalUploadSlot, item: FinalUploadItem, signal: AbortSignal | undefined): Promise<void> {
    if (slot.thumbUrl === null) {
      return;
    }

    let uri: string | null = null;
    try {
      const thumb = await deps.thumbs.render(item.file);
      if (thumb !== null) {
        uri = thumb.uri;
        await deps.storage.put({ url: slot.thumbUrl, headers: slot.thumbHeaders }, thumb.uri, { signal });
      }
    } catch (error) {
      if (isCancelled(signal)) {
        throw error;
      }
    } finally {
      if (uri !== null) {
        deps.staging.discard(uri);
      }
    }
  }

  /**
   * Sends the finals of a batch to the storage, with their thumbnails, a few at a time.
   * @param batch The finals of the batch.
   * @param slots The places the API gave, in the same order.
   * @param observer Told about every change.
   * @param signal Optional signal to cancel the uploads.
   * @returns The finals the storage has.
   */
  async function sendBatch(batch: readonly FinalUploadItem[], slots: readonly FinalUploadSlot[], observer: UploadObserver, signal: AbortSignal | undefined): Promise<FinalUploadItem[]> {
    const results = await mapWithLimit(batch, FINAL_UPLOAD_CONCURRENCY, async (item, index): Promise<FinalUploadItem | null> => {
      const slot = slots[index];
      if (slot === undefined) {
        observer.onItem(item.id, 'failed', describeUploadFailure(null));
        return null;
      }

      observer.onItem(item.id, 'uploading', null);
      try {
        await deps.storage.put(slot, item.file.uri, { onProgress: (fraction) => observer.onProgress(item.id, fraction), signal });
        await sendThumb(slot, item, signal);
        return item;
      } catch (error) {
        if (isCancelled(signal)) {
          throw error;
        }

        observer.onItem(item.id, 'failed', describeUploadFailure(error));
        return null;
      }
    });
    return results.filter((item): item is FinalUploadItem => item !== null);
  }

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
            batch.map((item) => ({ photoId: item.photoId, bytes: item.file.bytes })),
            signal,
          );
          sent = await sendBatch(batch, slots, observer, signal);
        } catch (error) {
          if (isCancelled(signal)) {
            return { stopped: true, problem: null };
          }

          const problem = describeUploadRequestError(error);
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
          const result = await deps.api.registerFinals(
            galleryId,
            sent.map((item) => item.photoId),
            sent.map((item) => ({ photoId: item.photoId, width: item.file.width, height: item.file.height, capturedAt: item.file.capturedAt })),
            signal,
          );
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

          const problem = describeUploadRequestError(error);
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
