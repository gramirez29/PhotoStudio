import { useMutation, useQuery, useQueryClient, type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { portalApi } from '../api/client';
import { ApiError } from '../api/httpClient';
import type { PortalGallery, PortalGallerySummary } from '../types/api/portal';
import type { SubmitSelectionVariables } from '../types/hooks/usePortalGallery.types';

/**
 * Key of the cached gallery of a link.
 * @param token Secret token taken from the URL.
 * @returns The query key.
 */
export function portalGalleryKey(token: string): readonly [string, string] {
  return ['portal-gallery', token];
}

/**
 * Asks where the gallery is, with the link alone. "There is no gallery for the client yet" is not a failure here: the API answers
 * 404 for a booking without a gallery or with one that still waits for its proofs, and that gives <c>null</c>, which shows nothing.
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query result: the summary, <c>null</c> when there is nothing to offer, or an error.
 */
export function useGallerySummary(token: string): UseQueryResult<PortalGallerySummary | null, Error> {
  return useQuery({
    queryKey: ['portal-gallery-summary', token],
    queryFn: async ({ signal }) => {
      try {
        return await portalApi.getGallerySummary(token, signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null;
        }

        throw error;
      }
    },
    retry: false,
  });
}

/**
 * Loads the gallery. The addresses of the photos expire, so it is dropped from the cache as soon as nothing shows it, and it is
 * never retried on its own: a refused session needs a new code.
 * @param token Secret token taken from the URL.
 * @param accessToken Token of the session obtained by verifying a code.
 * @returns The TanStack Query result.
 */
export function useGallery(token: string, accessToken: string): UseQueryResult<PortalGallery, Error> {
  return useQuery({
    queryKey: portalGalleryKey(token),
    queryFn: ({ signal }) => portalApi.getGallery(token, accessToken, signal),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
}

/**
 * Mutation that sends the selection. On success the gallery it answers with replaces the cached one, so the page shows the
 * selection as sent. It is never retried on its own: sending the same selection twice is harmless, but the client decides.
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query mutation.
 */
export function useSubmitSelection(token: string): UseMutationResult<PortalGallery, Error, SubmitSelectionVariables> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ accessToken, photoIds }: SubmitSelectionVariables) => portalApi.submitSelection(token, accessToken, photoIds),
    onSuccess: (gallery) => {
      queryClient.setQueryData(portalGalleryKey(token), gallery);
      void queryClient.invalidateQueries({ queryKey: ['portal-gallery-summary', token] });
    },
  });
}
