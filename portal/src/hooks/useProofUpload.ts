import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { browserProofUploader } from '../media/browserProofUploader';
import type { StudioGallery } from '../types/api/studio';
import type { ProofUpload } from '../types/hooks/useProofUpload.types';
import type { UploadItem, UploadObserver } from '../types/media/proofUploader.types';
import { describeSkipped, selectProofFiles } from '../utils/proofSelection';
import { galleryKey, proofsKey } from './useStudioGallery';

/**
 * Runs the upload of proofs of a gallery for a page. The person chooses files, sees a sample of how the proofs will look and
 * confirms; then the files are prepared in the browser, sent straight to the storage and registered, and the queue shows where each
 * one is. A photo that fails stays in the queue with its reason, to try again. Leaving the page cancels what is running.
 * @param gallery The gallery the proofs go to, as it is now (its count says how much room is left).
 * @returns The state of the upload and what can be done with it.
 */
export function useProofUpload(gallery: StudioGallery): ProofUpload {
  const queryClient = useQueryClient();
  const [items, setItems] = useState<readonly UploadItem[]>([]);
  const [running, setRunning] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState<readonly File[]>([]);
  const queue = useRef<readonly UploadItem[]>([]);
  const nextId = useRef(1);
  const controller = useRef<AbortController | null>(null);

  // What is running stops when the page is left: no photo is prepared or sent for a page nobody sees.
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
  const commit = useCallback((next: readonly UploadItem[]): void => {
    queue.current = next;
    setItems(next);
  }, []);

  /**
   * Runs the uploader over the photos in the queue that are not registered yet.
   * @param toRun The photos to upload.
   */
  const run = useCallback(
    async (toRun: readonly UploadItem[]): Promise<void> => {
      const abort = new AbortController();
      controller.current = abort;
      setRunning(true);
      setProblem(null);

      const observer: UploadObserver = {
        onItem: (id, status, itemProblem) => commit(queue.current.map((item) => (item.id === id ? { ...item, status, problem: itemProblem } : item))),
        onDone: (id) => commit(queue.current.filter((item) => item.id !== id)),
        onRegistered: (updated) => {
          queryClient.setQueryData(galleryKey(updated.id), updated);
          void queryClient.invalidateQueries({ queryKey: proofsKey(updated.id) });
        },
      };

      const outcome = await browserProofUploader.run(
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
      const room = gallery.maxPhotos - gallery.photoCount - queue.current.length;
      const selection = selectProofFiles(
        files,
        queue.current.map((item) => item.file),
        room,
      );
      setNotice(describeSkipped(selection));
      setPending(selection.accepted);
    },
    [gallery.maxPhotos, gallery.photoCount],
  );

  const confirm = useCallback((): void => {
    const created = pending.map((file): UploadItem => ({ id: nextId.current++, file, status: 'waiting', problem: null }));
    const all = [...queue.current.map((item): UploadItem => ({ ...item, status: 'waiting', problem: null })), ...created];
    setPending([]);
    commit(all);
    void run(all);
  }, [commit, pending, run]);

  const cancel = useCallback((): void => setPending([]), []);

  const retry = useCallback((): void => {
    const all = queue.current.map((item): UploadItem => ({ ...item, status: 'waiting', problem: null }));
    commit(all);
    void run(all);
  }, [commit, run]);

  const dismissFailed = useCallback((): void => commit(queue.current.filter((item) => item.status !== 'failed')), [commit]);

  return { items, running, problem, notice, pending, choose, confirm, cancel, retry, dismissFailed };
}
