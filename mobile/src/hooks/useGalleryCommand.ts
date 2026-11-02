import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { galleryApi } from '../api/client';
import type { GalleryResponse } from '../types/api/gallery';
import type { GalleryCommand } from '../types/hooks/useGalleryCommand.types';
import { GALLERY_QUERY_PREFIX, galleryKey } from './useGallery';

/**
 * Sends a command about a gallery to the API.
 * @param galleryId Gallery identifier.
 * @param command What the photographer asks.
 * @returns The gallery after the command.
 */
function execute(galleryId: string, command: GalleryCommand): Promise<GalleryResponse> {
  switch (command.kind) {
    case 'editTerms':
      return galleryApi.editTerms(galleryId, command.terms);
    case 'removeProof':
      return galleryApi.removeProof(galleryId, command.photoId);
    case 'openSelection':
      return galleryApi.openSelection(galleryId);
    case 'reopenSelection':
      return galleryApi.reopenSelection(galleryId, command.reason);
    case 'publishFinals':
      return galleryApi.publishFinals(galleryId);
    case 'releaseExtras':
      return galleryApi.releaseExtras(galleryId);
    case 'removeZip':
      return galleryApi.removeZip(galleryId);
    case 'release':
      return galleryApi.release(galleryId);
  }
}

/**
 * Mutation that runs a command about a gallery. The gallery it answers with replaces the cached one and everything else about
 * galleries is loaded again, because a command can change the selection, the proofs or the state. It is never retried on its own: some
 * commands send an email to the client.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query mutation.
 */
export function useGalleryCommand(galleryId: string): UseMutationResult<GalleryResponse, Error, GalleryCommand> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (command: GalleryCommand) => execute(galleryId, command),
    onSuccess: async (gallery) => {
      queryClient.setQueryData(galleryKey(gallery.id), gallery);
      await queryClient.invalidateQueries({ queryKey: GALLERY_QUERY_PREFIX });
    },
  });
}
