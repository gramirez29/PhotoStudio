import type { FolderWriter } from '../types/media/folderDownload.types';

/** The part of a directory handle of the File System Access API that the page uses. */
interface DirectoryHandle {
  getFileHandle(name: string, options: { create: boolean }): Promise<{ createWritable(): Promise<{ write(data: Blob): Promise<void>; close(): Promise<void> }> }>;
}

/** The window of a browser that can let a page write into a folder the person chooses. */
interface FolderPickerWindow {
  showDirectoryPicker?: (options: { mode: 'readwrite' }) => Promise<DirectoryHandle>;
}

/**
 * Tells whether this browser lets a page save files into a folder the person chooses (Chrome and Edge do; Firefox and Safari do not).
 * @returns True when the folder picker exists.
 */
export function isFolderPickerSupported(): boolean {
  return typeof (window as unknown as FolderPickerWindow).showDirectoryPicker === 'function';
}

/**
 * Asks the person to choose a folder to save files into. The browser asks for the permission to write there.
 * @returns The way to write in the folder, or null when the person closed the window without choosing.
 */
export async function chooseFolder(): Promise<FolderWriter | null> {
  const picker = (window as unknown as FolderPickerWindow).showDirectoryPicker;
  if (picker === undefined) {
    return null;
  }

  let directory: DirectoryHandle;
  try {
    directory = await picker.call(window, { mode: 'readwrite' });
  } catch {
    return null;
  }

  return {
    async writeFile(fileName, file) {
      const handle = await directory.getFileHandle(fileName, { create: true });
      const writable = await handle.createWritable();
      await writable.write(file);
      await writable.close();
    },
  };
}
