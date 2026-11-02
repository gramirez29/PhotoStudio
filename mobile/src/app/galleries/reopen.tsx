import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { GalleryReopenForm } from '../../components/GalleryReopenForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useGalleryById } from '../../hooks/useGallery';
import { galleryErrorMessage } from '../../utils/apiErrors';

/**
 * Screen to reopen the selection: loads the gallery of the address and shows it.
 * @returns The screen.
 */
export default function ScreentoreopentheselectionScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const galleryId = typeof id === 'string' ? id : '';
  const query = useGalleryById(galleryId);

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando la galería" />;
  }

  if (query.isError) {
    return <ScreenMessage message={galleryErrorMessage(query.error)} />;
  }

  return <GalleryReopenForm gallery={query.data} />;
}
