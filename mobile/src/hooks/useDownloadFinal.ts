import { useMutation, type UseMutationResult } from '@tanstack/react-query';
import { galleryApi } from '../api/client';
import { openExternalUrl } from '../navigation/externalLinks';

/**
 * Mutation that asks for the address of a final photo and opens it in the browser of the phone, which downloads it. It is not counted
 * as a download of the client. It rejects when the address cannot be had or the phone cannot open it.
 * @param galleryId Gallery identifier.
 * @returns The TanStack Query mutation; its variable is the identifier of the photo.
 */
export function useDownloadFinal(galleryId: string): UseMutationResult<void, Error, string> {
  return useMutation({
    mutationFn: async (photoId: string) => {
      const link = await galleryApi.getFinalDownload(galleryId, photoId);
      if (!(await openExternalUrl(link.url))) {
        throw new Error('The phone could not open the address.');
      }
    },
  });
}
