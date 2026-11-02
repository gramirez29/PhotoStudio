import type { ZipUploader, ZipUploaderDeps } from '../types/media/upload.types';
import { describeUploadRequestError, describeZipFailure } from '../utils/uploadMessages';

/**
 * Creates the uploader of the ZIP: the steps from a file on the phone to the ZIP offered to the client. The file goes straight to the
 * storage from where it is, without being read into memory, so a ZIP of hundreds of megabytes is not a problem for the phone.
 * @param deps What the uploader needs from outside.
 * @returns The uploader.
 */
export function createZipUploader(deps: ZipUploaderDeps): ZipUploader {
  return {
    async run(galleryId, file, onProgress, signal) {
      const cancelled = (): boolean => signal?.aborted === true;
      try {
        const slot = await deps.api.requestZipUpload(galleryId, file.bytes, signal);
        try {
          await deps.storage.put(slot, file.uri, { onProgress, signal });
        } catch (error) {
          return { gallery: null, problem: cancelled() ? null : describeZipFailure(error) };
        }

        return { gallery: await deps.api.registerZip(galleryId, signal), problem: null };
      } catch (error) {
        return { gallery: null, problem: cancelled() ? null : describeUploadRequestError(error) };
      }
    },
  };
}
