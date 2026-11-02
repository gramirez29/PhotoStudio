import { File, UploadType } from 'expo-file-system';
import { StorageUploadError } from '../api/storageUploadError';
import type { StorageUploader } from '../types/media/upload.types';

/**
 * Creates the uploader that sends files from the phone straight to the storage with a PUT to a signed address. The file is streamed
 * from where it is, never read into memory, and the progress is reported as it goes. The address and its headers are never logged.
 * @returns The uploader.
 */
export function createStorageUploader(): StorageUploader {
  return {
    async put(target, uri, options) {
      let status: number;
      try {
        const result = await new File(uri).upload(target.url, {
          httpMethod: 'PUT',
          uploadType: UploadType.BINARY_CONTENT,
          headers: { ...target.headers },
          onProgress: (progress) => options?.onProgress?.(progress.totalBytes > 0 ? Math.min(1, progress.bytesSent / progress.totalBytes) : 0),
          ...(options?.signal === undefined ? {} : { signal: options.signal }),
        });
        status = result.status;
      } catch {
        throw new StorageUploadError(null);
      }

      if (status < 200 || status >= 300) {
        throw new StorageUploadError(status);
      }
    },
  };
}
