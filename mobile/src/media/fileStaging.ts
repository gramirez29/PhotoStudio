import { Directory, File, Paths } from 'expo-file-system';
import { STAGING_FOLDER } from '../constants/upload';
import type { FileStaging } from '../types/media/upload.types';

/**
 * Tells whether a file is inside the cache of the app. The app only ever deletes what is there: what the photographer keeps in their
 * photo library or in their files is never touched, whatever address it is given.
 * @param uri Address of the file.
 * @returns True when the file is in the cache of the app.
 */
export function isInAppCache(uri: string): boolean {
  return uri.startsWith(Paths.cache.uri);
}

/**
 * Deletes a file of the cache of the app. It never fails, and it never deletes anything outside the cache.
 * @param uri Address of the file.
 */
export function discardCachedFile(uri: string): void {
  try {
    if (!isInAppCache(uri)) {
      return;
    }

    const file = new File(uri);
    if (file.exists) {
      file.delete();
    }
  } catch {
    // A file that is already gone, or that the system took away, is not a problem.
  }
}

/**
 * Creates the staging area of the files the app prepares: a folder of the cache of the phone, where a proof waits until it is sent.
 * The system may empty the cache when the phone runs out of space, and the files are deleted as soon as they are sent anyway.
 * @returns The staging area.
 */
export function createFileStaging(): FileStaging {
  let counter = 0;

  return {
    stage(bytes) {
      const folder = new Directory(Paths.cache, STAGING_FOLDER);
      if (!folder.exists) {
        folder.create({ intermediates: true });
      }

      counter += 1;
      const file = new File(folder, `proof-${Date.now()}-${counter}.jpg`);
      file.create({ overwrite: true });
      file.write(bytes);
      return { uri: file.uri, bytes: bytes.byteLength };
    },
    discard: discardCachedFile,
  };
}
