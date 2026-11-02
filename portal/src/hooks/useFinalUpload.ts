import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { browserFinalUploader } from '../media/browserFinalUploader';
import type { StudioGallery, StudioSelectedPhoto } from '../types/api/studio';
import type { FinalUpload } from '../types/hooks/useFinalUpload.types';
import type { FinalUploadItem } from '../types/media/finalUploader.types';
import type { UploadObserver } from '../types/media/proofUploader.types';
import type { FinalMatch } from '../types/utils/finalMatching.types';
import { describeSkippedFinals, matchFinals } from '../utils/finalMatching';
import { galleryKey, selectionKey } from './useStudioGallery';

/**
 * Runs the upload of final photos of a gallery for a page. The person chooses files; each is paired with the photo of the selection
 * it belongs to by its name, and after they confirm the files are sent straight to the storage and registered. A final that fails
 * stays in the queue with its reason, to try again. Leaving the page cancels what is running.
 * @param gallery The gallery the finals go to.
 * @param photos The photos the client chose, which the files are paired with.
 * @returns The state of the upload and what can be done with it.
 */
export function useFinalUpload(gallery: StudioGallery, photos: readonly StudioSelectedPhoto[]): FinalUpload {
  const queryClient = useQueryClient();
  const [items, setItems] = useState<readonly FinalUploadItem[]>([]);
  const [running, setRunning] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [staged, setStaged] = useState<readonly FinalMatch[]>([]);
  const queue = useRef<readonly FinalUploadItem[]>([]);
  const nextId = useRef(1);
  const controller = useRef<AbortController | null>(null);

  // What is running stops when the page is left.
  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  /**
   * Replaces the queue, in the reference the run reads and in the state the page shows.
   * @param next The new queue.
   */
  const commit = useCallback((next: readonly FinalUploadItem[]): void => {
    queue.current = next;
    setItems(next);
  }, []);

  /**
   * Runs the uploader over the finals of the queue.
   * @param toRun The finals to upload.
   */
  const run = useCallback(
    async (toRun: readonly FinalUploadItem[]): Promise<void> => {
      const abort = new AbortController();
      controller.current = abort;
      setRunning(true);
      setProblem(null);
      const observer: UploadObserver = {
        onItem: (id, status, itemProblem) => commit(queue.current.map((item) => (item.id === id ? { ...item, status, problem: itemProblem } : item))),
        onDone: (id) => commit(queue.current.filter((item) => item.id !== id)),
        onRegistered: (updated) => {
          queryClient.setQueryData(galleryKey(updated.id), updated);
          void queryClient.invalidateQueries({ queryKey: selectionKey(updated.id) });
        },
      };
      const outcome = await browserFinalUploader.run(
        gallery.id,
        toRun.map((item) => ({ ...item, status: 'waiting', problem: null })),
        observer,
        abort.signal,
      );
      if (!abort.signal.aborted) {
        setRunning(false);
        setProblem(outcome.problem);
      }
    },
    [commit, gallery.id, queryClient],
  );

  const choose = useCallback(
    (files: readonly File[]): void => {
      const queued = new Set(queue.current.map((item) => item.photoId));
      const selection = matchFinals(
        files,
        photos.filter((photo) => !queued.has(photo.id)),
      );
      setNotice(describeSkippedFinals(selection));
      setStaged(selection.matches);
    },
    [photos],
  );

  const confirm = useCallback((): void => {
    const created = staged.map((match): FinalUploadItem => ({ id: nextId.current++, file: match.file, photoId: match.photoId, status: 'waiting', problem: null }));
    const all = [...queue.current.map((item): FinalUploadItem => ({ ...item, status: 'waiting', problem: null })), ...created];
    setStaged([]);
    commit(all);
    void run(all);
  }, [commit, run, staged]);

  const cancel = useCallback((): void => setStaged([]), []);

  const retry = useCallback((): void => {
    const all = queue.current.map((item): FinalUploadItem => ({ ...item, status: 'waiting', problem: null }));
    commit(all);
    void run(all);
  }, [commit, run]);

  const dismissFailed = useCallback((): void => commit(queue.current.filter((item) => item.status !== 'failed')), [commit]);

  return { staged, items, running, problem, notice, choose, confirm, cancel, retry, dismissFailed };
}
