import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { studioApi } from '../api/client';
import { ProofUploadError, putProof } from '../api/proofStorage';
import { ZIP_MAX_BYTES } from '../constants/finals';
import type { StudioGallery } from '../types/api/studio';
import type { ZipUpload, ZipUploadPhase } from '../types/hooks/useZipUpload.types';
import { describeFinalsRequestError } from '../utils/proofMessages';
import { isZipFile } from '../utils/zipFile';
import { galleryKey } from './useStudioGallery';
import { useSessionGuard } from './useSessionGuard';

/**
 * Uploads the ZIP with the photos, which the photographer made by hand: the file goes straight to the storage in one request and
 * then it is registered. There is no progress bar, because a request does not report how much it has sent, so the page says it can
 * take several minutes. Leaving the page cancels what is running.
 * @param gallery The gallery the ZIP belongs to.
 * @returns The state of the upload and the way to start it.
 */
export function useZipUpload(gallery: StudioGallery): ZipUpload {
  const queryClient = useQueryClient();
  const guard = useSessionGuard();
  const [phase, setPhase] = useState<ZipUploadPhase>('idle');
  const [problem, setProblem] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);

  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  const upload = useCallback(
    async (file: File): Promise<void> => {
      setProblem(null);
      if (!isZipFile(file)) {
        setProblem('El archivo no es un ZIP.');
        return;
      }

      if (file.size < 1 || file.size > ZIP_MAX_BYTES) {
        setProblem(file.size < 1 ? 'El archivo está vacío.' : 'El ZIP pesa más de 500 MB. Hazlo más liviano o divídelo.');
        return;
      }

      const abort = new AbortController();
      controller.current = abort;
      try {
        setPhase('uploading');
        const slot = await guard(() => studioApi.requestZipUpload(gallery.id, file.size, abort.signal));
        await putProof(slot, file, abort.signal);
        setPhase('registering');
        const updated = await guard(() => studioApi.registerZip(gallery.id, abort.signal));
        queryClient.setQueryData(galleryKey(updated.id), updated);
      } catch (error) {
        if (!abort.signal.aborted) {
          setProblem(error instanceof ProofUploadError ? 'No se pudo subir el ZIP. Si tardó mucho, el permiso para subirlo pudo vencer: vuelve a intentarlo.' : describeFinalsRequestError(error));
        }
      } finally {
        if (!abort.signal.aborted) {
          setPhase('idle');
        }
      }
    },
    [gallery.id, guard, queryClient],
  );

  return { phase, problem, upload };
}
