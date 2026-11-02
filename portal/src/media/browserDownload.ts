import type { DownloadItem } from '../types/utils/downloads.types';

/**
 * Starts the download of a file in the browser. The address answers with `Content-Disposition: attachment`, so the browser saves the
 * file and the page stays where it is. No referrer is sent: the storage does not need to know where the client is.
 * @param item The file.
 */
export function triggerDownload(item: DownloadItem): void {
  const anchor = document.createElement('a');
  anchor.href = item.url;
  anchor.download = item.fileName;
  anchor.rel = 'noopener noreferrer';
  anchor.referrerPolicy = 'no-referrer';
  anchor.style.display = 'none';
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
}
