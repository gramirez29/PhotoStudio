import { useCallback, useEffect, useRef, useState } from 'react';
import { studioApi } from '../api/client';
import { DOWNLOAD_PAUSE_MS } from '../constants/finals';
import { chooseFolder, isFolderPickerSupported } from '../media/browserFolder';
import { triggerDownload } from '../media/browserDownload';
import { saveFinalsToFolder } from '../media/folderDownload';
import type { StudioGallery } from '../types/api/studio';
import type { FolderDownload, FolderDownloadPhase } from '../types/hooks/useFolderDownload.types';
import type { FolderDownloadFailure } from '../types/media/folderDownload.types';
import { describeFinalsRequestError } from '../utils/proofMessages';
import { downloadInSequence } from '../utils/downloads';
import { useSessionGuard } from './useSessionGuard';

/**
 * Runs the saving of the final photos of a gallery for a card. In a browser that can write into a folder (Chrome, Edge) the
 * photographer chooses the folder and every final is received, its size is compared with the one recorded and it is written there. In
 * any other browser the downloads are started one by one, which cannot be verified. Leaving the page stops what is running.
 * @param gallery The gallery.
 * @returns The saving and what can be done with it.
 */
export function useFolderDownload(gallery: StudioGallery): FolderDownload {
  const guard = useSessionGuard();
  const [phase, setPhase] = useState<FolderDownloadPhase>('idle');
  const [handled, setHandled] = useState(0);
  const [total, setTotal] = useState(0);
  const [saved, setSaved] = useState(0);
  const [failed, setFailed] = useState<readonly FolderDownloadFailure[]>([]);
  const [problem, setProblem] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const canUseFolder = isFolderPickerSupported();

  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  const start = useCallback((): void => {
    const abort = new AbortController();
    controller.current = abort;

    void (async () => {
      setProblem(null);
      setFailed([]);
      setHandled(0);
      setSaved(0);
      try {
        const selection = await guard(() => studioApi.getSelection(gallery.id, abort.signal));
        const items = selection.photos.flatMap((photo) => (photo.finalBytes === null ? [] : [{ photoId: photo.id, name: photo.name, bytes: photo.finalBytes }]));
        setTotal(items.length);
        if (items.length === 0) {
          setPhase('done');
          return;
        }

        if (!canUseFolder) {
          setPhase('running');
          const links = await Promise.all(items.map((item) => guard(() => studioApi.getFinalDownload(gallery.id, item.photoId, abort.signal))));
          await downloadInSequence(links, triggerDownload, DOWNLOAD_PAUSE_MS, setHandled);
          setSaved(items.length);
          setPhase('started');
          return;
        }

        const folder = await chooseFolder();
        if (folder === null) {
          setPhase('idle');
          return;
        }

        setPhase('running');
        const result = await saveFinalsToFolder(
          items,
          {
            getLink: (photoId) => guard(() => studioApi.getFinalDownload(gallery.id, photoId, abort.signal)),
            fetchFile: async (url) => {
              const response = await fetch(url, { credentials: 'omit', referrerPolicy: 'no-referrer', signal: abort.signal });
              if (!response.ok) {
                throw new Error('The storage did not answer with success.');
              }

              return response.blob();
            },
            writeFile: (fileName, file) => folder.writeFile(fileName, file),
          },
          setHandled,
          abort.signal,
        );
        if (abort.signal.aborted) {
          return;
        }

        setSaved(result.saved);
        setFailed(result.failed);
        setPhase(result.failed.length === 0 ? 'done' : 'problem');
      } catch (error) {
        if (!abort.signal.aborted) {
          setProblem(describeFinalsRequestError(error));
          setPhase('problem');
        }
      }
    })();
  }, [canUseFolder, gallery.id, guard]);

  return { phase, handled, total, saved, failed, problem, canUseFolder, start };
}
