import type { PortalDownload, PortalFinal } from '../types/api/portal';
import type { DownloadItem } from '../types/utils/downloads.types';

/**
 * Waits for a time.
 * @param milliseconds How long to wait.
 * @returns A promise that resolves when the time passed.
 */
export function sleep(milliseconds: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, milliseconds);
  });
}

/**
 * Turns the addresses the client asked for into what the browser needs to download them.
 * @param downloads The addresses.
 * @returns The address and file name of each.
 */
export function toDownloads(downloads: readonly Pick<PortalDownload, 'url' | 'fileName'>[]): readonly DownloadItem[] {
  return downloads.map((download) => ({ url: download.url, fileName: download.fileName }));
}

/**
 * Downloads files one after another with a pause between them, because a browser that is asked for many files at once drops some.
 * It tells how many were started after each one.
 * @param items The files.
 * @param trigger Starts the download of one file in the browser.
 * @param pauseMs Pause between one file and the next.
 * @param onProgress Told how many files were started so far.
 * @param wait Waits for the pause; tests replace it.
 * @returns A promise that resolves when every download was started.
 */
export async function downloadInSequence(
  items: readonly DownloadItem[],
  trigger: (item: DownloadItem) => void,
  pauseMs: number,
  onProgress: (started: number) => void,
  wait: (milliseconds: number) => Promise<void> = sleep,
): Promise<void> {
  for (const [index, item] of items.entries()) {
    trigger(item);
    onProgress(index + 1);
    if (index < items.length - 1) {
      await wait(pauseMs);
    }
  }
}

/**
 * Adds up the size of some finals.
 * @param finals The finals.
 * @returns Their total size in bytes.
 */
export function totalBytes(finals: readonly Pick<PortalFinal, 'bytes'>[]): number {
  return finals.reduce((sum, final) => sum + final.bytes, 0);
}
