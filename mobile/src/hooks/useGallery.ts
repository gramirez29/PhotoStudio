import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { galleryApi } from '../api/client';
import { ApiError } from '../api/httpClient';
import type { GalleryResponse, ProofResponse, SelectionResponse } from '../types/api/gallery';

/** Prefix of every query about galleries, so one call can drop them all. */
export const GALLERY_QUERY_PREFIX = ['gallery'] as const;

/** How long the signed addresses of the small images stay good before they are asked for again. */
const ADDRESS_STALE_MS = 4 * 60 * 1000;

/**
 * Builds the key of the gallery of a booking.
 * @param bookingId Booking identifier.
 * @returns The key.
 */
export function galleryOfBookingKey(bookingId: string): readonly ['gallery', 'booking', string] {
  return ['gallery', 'booking', bookingId] as const;
}

/**
 * Builds the key of a gallery.
 * @param galleryId Gallery identifier.
 * @returns The key.
 */
export function galleryKey(galleryId: string): readonly ['gallery', 'id', string] {
  return ['gallery', 'id', galleryId] as const;
}

/**
 * Loads the gallery of a booking. A booking that has none (it was not completed yet) answers 404, which is "nothing to show", not a
 * failure.
 * @param bookingId Booking identifier.
 * @param enabled Whether to ask: only a completed booking can have a gallery.
 * @returns The TanStack Query result: the gallery, or null when there is none.
 */
export function useGalleryOfBooking(bookingId: string, enabled: boolean): UseQueryResult<GalleryResponse | null, Error> {
  return useQuery({
    queryKey: galleryOfBookingKey(bookingId),
    queryFn: async ({ signal }) => {
      try {
        return await galleryApi.getByBooking(bookingId, signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null;
        }

        throw error;
      }
    },
    enabled: enabled && bookingId.length > 0,
  });
}

/**
 * Loads a gallery.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query result.
 */
export function useGalleryById(galleryId: string): UseQueryResult<GalleryResponse, Error> {
  return useQuery({
    queryKey: galleryKey(galleryId),
    queryFn: ({ signal }) => galleryApi.get(galleryId, signal),
    enabled: galleryId.length > 0,
  });
}

/**
 * Loads what the client chose, with the small image of each photo. The addresses expire, so the list is asked for again after a
 * few minutes.
 * @param galleryId Gallery identifier.
 * @param enabled Whether to ask: only a gallery whose client sent the selection has one.
 * @returns The TanStack Query result.
 */
export function useGallerySelection(galleryId: string, enabled: boolean): UseQueryResult<SelectionResponse, Error> {
  return useQuery({
    queryKey: ['gallery', 'selection', galleryId] as const,
    queryFn: ({ signal }) => galleryApi.getSelection(galleryId, signal),
    enabled: enabled && galleryId.length > 0,
    staleTime: ADDRESS_STALE_MS,
  });
}

/**
 * Loads the proofs of a gallery, each with an address that works for a few minutes.
 * @param galleryId Gallery identifier.
 * @param enabled Whether to ask: the grid of proofs is loaded only when the photographer asks to see it, because it is a lot of images.
 * @returns The TanStack Query result.
 */
export function useGalleryProofs(galleryId: string, enabled: boolean): UseQueryResult<readonly ProofResponse[], Error> {
  return useQuery({
    queryKey: ['gallery', 'proofs', galleryId] as const,
    queryFn: ({ signal }) => galleryApi.listProofs(galleryId, signal),
    enabled: enabled && galleryId.length > 0,
    staleTime: ADDRESS_STALE_MS,
  });
}

/**
 * Asks for the address of the real photo, as it was edited, to show it large. It is asked for only for the photo that is open, so only
 * one photo of several megabytes is loaded at a time.
 * @param galleryId Gallery identifier.
 * @param photoId Photo identifier.
 * @param enabled Whether to ask: only a photo that has a final.
 * @returns The TanStack Query result.
 */
export function useFinalView(galleryId: string, photoId: string, enabled: boolean): UseQueryResult<string, Error> {
  return useQuery({
    queryKey: ['gallery', 'final-view', galleryId, photoId] as const,
    queryFn: async ({ signal }) => (await galleryApi.getFinalDownload(galleryId, photoId, signal)).url,
    enabled: enabled && galleryId.length > 0 && photoId.length > 0,
    staleTime: ADDRESS_STALE_MS,
  });
}
