import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ZIP_MAX_BYTES } from '../constants/upload';
import { pickZip } from '../media/devicePickers';
import { deviceZipUploader, discardPickedFile } from '../media/deviceUploaders';
import type { GalleryResponse } from '../types/api/gallery';
import type { ZipUpload } from '../types/hooks/useUpload.types';
import type { PickedFile } from '../types/utils/upload.types';
import { isZipFile } from '../utils/uploadFiles';
import { GALLERY_QUERY_PREFIX, galleryKey } from './useGallery';

/**
 * Runs the upload of the ZIP of a gallery for a screen. The photographer chooses the ZIP from the files of the phone, confirms, and
 * the file goes straight to the storage with its progress. A ZIP that is not a ZIP, or weighs nothing or more than 500 MB, is not
 * accepted before it is sent. Leaving the screen cancels what is being sent.
 * @param gallery The gallery the ZIP belongs to.
 * @returns The state of the upload and what can be done with it.
 */
export function useZipUpload(gallery: GalleryResponse): ZipUpload {
  const queryClient = useQueryClient();
  const [file, setFile] = useState<PickedFile | null>(null);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [problem, setProblem] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);

  // What is being sent stops when the screen is left.
  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  const choose = useCallback(async (): Promise<void> => {
    setProblem(null);
    const chosen = await pickZip();
    if (chosen === null) {
      return;
    }

    if (!isZipFile(chosen)) {
      setProblem('Ese archivo no es un ZIP.');
    } else if (chosen.bytes < 1 || chosen.bytes > ZIP_MAX_BYTES) {
      setProblem(chosen.bytes < 1 ? 'Ese ZIP está vacío.' : 'Ese ZIP pesa más de 500 MB.');
    } else {
      setFile(chosen);
    }
  }, []);

  const confirm = useCallback((): void => {
    if (file === null) {
      return;
    }

    const abort = new AbortController();
    controller.current = abort;
    setUploading(true);
    setProgress(0);
    setProblem(null);
    void deviceZipUploader.run(gallery.id, file, setProgress, abort.signal).then((outcome) => {
      if (abort.signal.aborted) {
        return;
      }

      setUploading(false);
      if (outcome.gallery === null) {
        setProblem(outcome.problem);
        return;
      }

      discardPickedFile(file.uri);
      setFile(null);
      queryClient.setQueryData(galleryKey(outcome.gallery.id), outcome.gallery);
      void queryClient.invalidateQueries({ queryKey: GALLERY_QUERY_PREFIX });
    });
  }, [file, gallery.id, queryClient]);

  const cancel = useCallback((): void => {
    controller.current?.abort();
    setUploading(false);
    setProgress(0);
    if (file !== null) {
      discardPickedFile(file.uri);
    }

    setFile(null);
  }, [file]);

  return { file, uploading, progress, problem, choose, confirm, cancel };
}
