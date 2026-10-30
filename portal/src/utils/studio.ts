import type { GalleryStatus, StudioGallery } from '../types/api/studio';
import type { GalleryStatusView } from '../types/utils/studio.types';
import { formatMoney } from './format';

/** The state of a gallery in words, and the tone of its dot. */
const STATUS_VIEWS: Readonly<Record<GalleryStatus, GalleryStatusView>> = {
  Pending: { label: 'Esperando las pruebas', tone: 'warning' },
  InSelection: { label: 'El cliente está eligiendo', tone: 'warning' },
  InEditing: { label: 'En edición', tone: 'neutral' },
  Ready: { label: 'Fotos listas', tone: 'success' },
  Delivered: { label: 'Entregada', tone: 'success' },
  Archived: { label: 'Archivada', tone: 'neutral' },
};

/**
 * Says in words where a gallery is.
 * @param status State of the gallery.
 * @returns The text and the tone of the dot.
 */
export function describeGalleryStatus(status: GalleryStatus): GalleryStatusView {
  return STATUS_VIEWS[status];
}

/**
 * Writes the photos the client may pick, for example "30 fotos incluidas · 5 de regalo".
 * @param gallery The gallery.
 * @returns The text; the gift is mentioned only when there is one.
 */
export function describePhotos(gallery: StudioGallery): string {
  const included = `${gallery.includedPhotos} ${gallery.includedPhotos === 1 ? 'foto incluida' : 'fotos incluidas'}`;
  return gallery.bonusPhotos > 0 ? `${included} · ${gallery.bonusPhotos} de regalo` : included;
}

/**
 * Writes what an extra photo costs, for example "Foto extra: ₡3 000,00".
 * @param gallery The gallery.
 * @returns The text.
 */
export function describeExtraPhoto(gallery: StudioGallery): string {
  return `Foto extra: ${formatMoney(gallery.extraPhotoPrice)}`;
}
