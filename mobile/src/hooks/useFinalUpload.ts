import { useCallback, useState } from 'react';
import { pickPhotos } from '../media/devicePickers';
import { deviceFinalUploader, discardPickedFile } from '../media/deviceUploaders';
import type { GalleryResponse, SelectedPhoto } from '../types/api/gallery';
import type { FinalUpload } from '../types/hooks/useUpload.types';
import type { FinalUploadItem } from '../types/media/upload.types';
import type { FinalMatch } from '../types/utils/upload.types';
import { describeSkippedFinals, matchFinals } from '../utils/finalMatching';
import { useUploadQueue } from './useUploadQueue';

/**
 * Runs the upload of final photos of a gallery for a screen. The photographer chooses the edited photos from the library; each is
 * paired by its file name with a photo the client chose, and the ones that match go up as they are (nothing is reduced or marked:
 * what the photographer edited is the product) with their thumbnails, and the queue shows where each one is.
 * @param gallery The gallery the finals belong to.
 * @param photos The photos the client chose, whose names the files are paired with.
 * @returns The state of the upload and what can be done with it.
 */
export function useFinalUpload(gallery: GalleryResponse, photos: readonly SelectedPhoto[]): FinalUpload {
  const queue = useUploadQueue<FinalUploadItem>(gallery.id, deviceFinalUploader.run);
  const [pending, setPending] = useState<readonly FinalMatch[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [preparing, setPreparing] = useState(false);

  const choose = useCallback(async (): Promise<void> => {
    setPreparing(true);
    try {
      const chosen = await pickPhotos(Math.max(1, photos.length));
      const selection = matchFinals(chosen, photos);
      setPending(selection.matches);
      setNotice(chosen.length === 0 ? null : describeSkippedFinals(selection));
    } finally {
      setPreparing(false);
    }
  }, [photos]);

  const confirm = useCallback((): void => {
    const created = pending.map((match): FinalUploadItem => ({ id: queue.newId(), file: match.file, photoId: match.photoId, status: 'waiting', problem: null, progress: 0 }));
    setPending([]);
    setNotice(null);
    queue.start(created);
  }, [pending, queue]);

  const cancel = useCallback((): void => {
    for (const match of pending) {
      discardPickedFile(match.file.uri);
    }

    setPending([]);
    setNotice(null);
  }, [pending]);

  return { queue, pending, notice, preparing, choose, confirm, cancel };
}
