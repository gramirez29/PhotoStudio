import type { FolderDownloadDeps, FolderDownloadFailure, FolderDownloadItem, FolderDownloadResult } from '../types/media/folderDownload.types';

/**
 * Gives a name that is not taken yet: two finals that come with the same name must not overwrite each other in the folder.
 * @param fileName Name the final comes with.
 * @param used Names already given, in lowercase; the one returned is added.
 * @returns The name to save the file with.
 */
export function uniqueFileName(fileName: string, used: Set<string>): string {
  const dot = fileName.lastIndexOf('.');
  const stem = dot > 0 ? fileName.slice(0, dot) : fileName;
  const extension = dot > 0 ? fileName.slice(dot) : '';
  let candidate = fileName;
  for (let count = 2; used.has(candidate.toLowerCase()); count++) {
    candidate = `${stem}-${count}${extension}`;
  }

  used.add(candidate.toLowerCase());
  return candidate;
}

/**
 * Saves the final photos of a gallery into a folder, one after another so the phone or computer holds a single photo at a time.
 * Each final is received from its signed address and its size is compared with the one the API recorded when it was uploaded: a
 * photo that arrives with another size is reported and not trusted. A failure of one photo does not stop the others.
 * @param items The finals to save.
 * @param deps What the saving needs from outside.
 * @param onProgress Told how many finals were handled so far.
 * @param signal Optional signal that stops the saving between two photos.
 * @returns How many were saved and which were not.
 */
export async function saveFinalsToFolder(
  items: readonly FolderDownloadItem[],
  deps: FolderDownloadDeps,
  onProgress: (handled: number) => void,
  signal?: AbortSignal,
): Promise<FolderDownloadResult> {
  const used = new Set<string>();
  const failed: FolderDownloadFailure[] = [];
  let saved = 0;

  for (const [index, item] of items.entries()) {
    if (signal?.aborted === true) {
      break;
    }

    try {
      const link = await deps.getLink(item.photoId);
      const file = await deps.fetchFile(link.url);
      if (file.size === item.bytes) {
        await deps.writeFile(uniqueFileName(link.fileName, used), file);
        saved++;
      } else {
        failed.push({ photoId: item.photoId, name: item.name, reason: 'size' });
      }
    } catch {
      failed.push({ photoId: item.photoId, name: item.name, reason: 'failed' });
    }

    onProgress(index + 1);
  }

  return { saved, failed };
}
