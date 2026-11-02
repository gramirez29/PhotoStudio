import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { discardPickedFile } from '../media/deviceUploaders';
import type { UploadItem, UploadObserver } from '../types/media/upload.types';
import type { UploadQueue, UploadRunner } from '../types/hooks/useUpload.types';
import { GALLERY_QUERY_PREFIX, galleryKey } from './useGallery';

/**
 * Puts a photo back to waiting, to try it again.
 * @param item The photo of the queue.
 * @returns The photo, waiting and without a problem.
 */
function waiting<T extends UploadItem>(item: T): T {
  return { ...item, status: 'waiting', problem: null, progress: 0 };
}

/**
 * Keeps the queue of an upload for a screen: the photographer adds photos, an uploader prepares, sends and registers them, and the
 * queue shows where each one is. A photo that fails stays in the queue with its reason, to try again. When a batch is registered the
 * gallery in the cache is replaced and everything else about galleries is loaded again. A photo that was registered leaves the queue
 * and its copy in the cache of the phone is deleted. Leaving the screen cancels what is running.
 * @param galleryId Gallery the photos go to.
 * @param runUploader The uploader that runs over the photos.
 * @returns The queue and what can be done with it.
 */
export function useUploadQueue<T extends UploadItem>(galleryId: string, runUploader: UploadRunner<T>): UploadQueue<T> {
  const queryClient = useQueryClient();
  const [items, setItems] = useState<readonly T[]>([]);
  const [running, setRunning] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const queue = useRef<readonly T[]>([]);
  const nextId = useRef(1);
  const controller = useRef<AbortController | null>(null);

  // What is running stops when the screen is left: no photo is prepared or sent for a screen nobody sees.
  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  /**
   * Replaces the queue, in the reference the run reads and in the state the screen shows.
   * @param next The new queue.
   */
  const commit = useCallback((next: readonly T[]): void => {
    queue.current = next;
    setItems(next);
  }, []);

  /**
   * Runs the uploader over photos.
   * @param toRun The photos to upload, all waiting.
   */
  const run = useCallback(
    async (toRun: readonly T[]): Promise<void> => {
      const abort = new AbortController();
      controller.current = abort;
      setRunning(true);
      setProblem(null);

      const observer: UploadObserver = {
        onItem: (id, status, itemProblem) => commit(queue.current.map((item) => (item.id === id ? { ...item, status, problem: itemProblem, progress: 0 } : item))),
        onProgress: (id, fraction) => commit(queue.current.map((item) => (item.id === id ? { ...item, progress: fraction } : item))),
        onDone: (id) => {
          const done = queue.current.find((item) => item.id === id);
          if (done !== undefined) {
            discardPickedFile(done.file.uri);
          }

          commit(queue.current.filter((item) => item.id !== id));
        },
        onRegistered: (gallery) => {
          queryClient.setQueryData(galleryKey(gallery.id), gallery);
          void queryClient.invalidateQueries({ queryKey: GALLERY_QUERY_PREFIX });
        },
      };

      const outcome = await runUploader(galleryId, toRun, observer, abort.signal);
      if (!abort.signal.aborted) {
        setRunning(false);
        setProblem(outcome.problem);
      }
    },
    [commit, galleryId, queryClient, runUploader],
  );

  const start = useCallback(
    (created: readonly T[]): void => {
      const all = [...queue.current.map(waiting), ...created];
      commit(all);
      void run(all);
    },
    [commit, run],
  );

  const retry = useCallback((): void => {
    const all = queue.current.map(waiting);
    commit(all);
    void run(all);
  }, [commit, run]);

  const dismissFailed = useCallback((): void => commit(queue.current.filter((item) => item.status !== 'failed')), [commit]);
  const newId = useCallback((): number => nextId.current++, []);

  return { items, running, problem, newId, start, retry, dismissFailed };
}
