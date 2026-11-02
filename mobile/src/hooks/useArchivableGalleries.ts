import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { galleryApi } from '../api/client';
import type { GalleryResponse } from '../types/api/gallery';
import { GALLERY_QUERY_PREFIX } from './useGallery';

/**
 * Loads the galleries whose space the photographer can free: the delivered ones, the one delivered longest ago first. Its key lives
 * under the one of every gallery query, so releasing a gallery (which reloads them all) takes it off the list.
 * @returns The TanStack Query result.
 */
export function useArchivableGalleries(): UseQueryResult<readonly GalleryResponse[], Error> {
  return useQuery({
    queryKey: [...GALLERY_QUERY_PREFIX, 'archivable'],
    queryFn: ({ signal }) => galleryApi.listArchivable(signal),
  });
}
