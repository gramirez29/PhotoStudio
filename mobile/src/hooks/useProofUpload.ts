import { useCallback, useEffect, useRef, useState } from 'react';
import { deviceProofUploader, discardPickedFile, makeProofSample } from '../media/deviceUploaders';
import { pickPhotos } from '../media/devicePickers';
import type { GalleryResponse } from '../types/api/gallery';
import type { ProofUpload } from '../types/hooks/useUpload.types';
import type { UploadItem } from '../types/media/upload.types';
import type { PickedFile } from '../types/utils/upload.types';
import { describeSkippedProofs, selectProofFiles } from '../utils/uploadFiles';
import { useUploadQueue } from './useUploadQueue';

/** Says the sample could not be made, which usually means the first photo is not a photo the phone can open. */
const SAMPLE_PROBLEM = 'No se pudo preparar la muestra con la primera foto. Revisa que sea un JPEG que no esté dañado.';

/**
 * Runs the upload of proofs of a gallery for a screen. The photographer chooses photos from the library, sees a sample of how the
 * proofs will look (made with the first photo) and confirms; then each photo is reduced and marked on the phone, sent straight to the
 * storage and registered, and the queue shows where each one is.
 * @param gallery The gallery the proofs go to, as it is now (its count says how much room is left).
 * @returns The state of the upload and what can be done with it.
 */
export function useProofUpload(gallery: GalleryResponse): ProofUpload {
  const queue = useUploadQueue<UploadItem>(gallery.id, deviceProofUploader.run);
  const [pending, setPending] = useState<readonly PickedFile[]>([]);
  const [sample, setSample] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [preparing, setPreparing] = useState(false);
  const sampleRef = useRef<string | null>(null);

  /**
   * Shows or forgets the sample, deleting the file of the one that goes away.
   * @param next Address of the new sample, or null.
   */
  const changeSample = useCallback((next: string | null): void => {
    if (sampleRef.current !== null) {
      discardPickedFile(sampleRef.current);
    }

    sampleRef.current = next;
    setSample(next);
  }, []);

  // The sample is a file of the cache: it is deleted when the screen is left.
  useEffect(
    () => () => {
      if (sampleRef.current !== null) {
        discardPickedFile(sampleRef.current);
      }
    },
    [],
  );

  const choose = useCallback(async (): Promise<void> => {
    const room = gallery.maxPhotos - gallery.photoCount - queue.items.length;
    if (room <= 0) {
      setNotice('La galería ya tiene el máximo de pruebas.');
      return;
    }

    setPreparing(true);
    try {
      const chosen = await pickPhotos(room);
      const selection = selectProofFiles(
        chosen,
        queue.items.map((item) => item.file),
        room,
      );
      const skipped = describeSkippedProofs(selection);
      const first = selection.accepted[0];
      setPending(selection.accepted);
      if (first === undefined) {
        changeSample(null);
        setNotice(skipped);
        return;
      }

      try {
        changeSample(await makeProofSample(first.uri));
        setNotice(skipped);
      } catch {
        changeSample(null);
        setNotice(skipped === null ? SAMPLE_PROBLEM : `${skipped} ${SAMPLE_PROBLEM}`);
      }
    } finally {
      setPreparing(false);
    }
  }, [changeSample, gallery.maxPhotos, gallery.photoCount, queue.items]);

  const confirm = useCallback((): void => {
    const created = pending.map((file): UploadItem => ({ id: queue.newId(), file, status: 'waiting', problem: null, progress: 0 }));
    setPending([]);
    setNotice(null);
    changeSample(null);
    queue.start(created);
  }, [changeSample, pending, queue]);

  const cancel = useCallback((): void => {
    for (const file of pending) {
      discardPickedFile(file.uri);
    }

    setPending([]);
    setNotice(null);
    changeSample(null);
  }, [changeSample, pending]);

  return { queue, pending, sample, notice, preparing, choose, confirm, cancel };
}
