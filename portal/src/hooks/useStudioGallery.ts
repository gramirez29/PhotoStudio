import { useMutation, useQuery, useQueryClient, type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { studioApi } from '../api/client';
import { PROOF_LIST_STALE_MS } from '../constants/proof';
import type { StudioProof } from '../types/api/proofs';
import type { StudioGallery } from '../types/api/studio';
import { STUDIO_DATA_PREFIX } from './useStudioSession';
import { useSessionGuard } from './useSessionGuard';

/**
 * Key of one gallery in the cache. It is under the prefix of the photographer's data, so signing out drops it.
 * @param galleryId Gallery identifier.
 * @returns The key.
 */
export function galleryKey(galleryId: string): readonly unknown[] {
  return [...STUDIO_DATA_PREFIX, 'detail', galleryId];
}

/**
 * Key of the proofs of one gallery in the cache.
 * @param galleryId Gallery identifier.
 * @returns The key.
 */
export function proofsKey(galleryId: string): readonly unknown[] {
  return [...STUDIO_DATA_PREFIX, 'proofs', galleryId];
}

/**
 * Loads one gallery of the signed-in photographer. A gallery of another photographer and one that does not exist answer the same
 * 404, so the page cannot tell them apart either.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query result.
 */
export function useStudioGallery(galleryId: string): UseQueryResult<StudioGallery, Error> {
  const guard = useSessionGuard();
  return useQuery({ queryKey: galleryKey(galleryId), queryFn: ({ signal }) => guard(() => studioApi.getGallery(galleryId, signal)) });
}

/**
 * Loads the proofs of a gallery. Each comes with an address that works for a few minutes, so the list is asked again a little
 * before they stop working and whenever the person comes back to the tab.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query result.
 */
export function useStudioProofs(galleryId: string): UseQueryResult<readonly StudioProof[], Error> {
  const guard = useSessionGuard();
  return useQuery({
    queryKey: proofsKey(galleryId),
    queryFn: ({ signal }) => guard(() => studioApi.listProofs(galleryId, signal)),
    staleTime: PROOF_LIST_STALE_MS,
  });
}

/**
 * Mutation that removes a proof. The gallery it answers with replaces the cached one and the list of proofs is loaded again. It is
 * never retried on its own.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query mutation; its variable is the identifier of the proof.
 */
export function useRemoveProof(galleryId: string): UseMutationResult<StudioGallery, Error, string> {
  const queryClient = useQueryClient();
  const guard = useSessionGuard();

  return useMutation({
    mutationFn: (photoId: string) => guard(() => studioApi.removeProof(galleryId, photoId)),
    onSuccess: (gallery) => {
      queryClient.setQueryData(galleryKey(galleryId), gallery);
      void queryClient.invalidateQueries({ queryKey: proofsKey(galleryId) });
    },
  });
}

/**
 * Mutation that opens the selection: the proofs are published and the client is told by email. The gallery it answers with
 * replaces the cached one, so the page shows it as in selection. It is never retried on its own: it sends an email.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query mutation.
 */
export function useOpenSelection(galleryId: string): UseMutationResult<StudioGallery, Error, void> {
  const queryClient = useQueryClient();
  const guard = useSessionGuard();

  return useMutation({
    mutationFn: () => guard(() => studioApi.openSelection(galleryId)),
    onSuccess: (gallery) => {
      queryClient.setQueryData(galleryKey(galleryId), gallery);
      // The list shows the state of each gallery, and this one just changed.
      void queryClient.invalidateQueries({ queryKey: STUDIO_DATA_PREFIX, exact: true });
    },
  });
}
