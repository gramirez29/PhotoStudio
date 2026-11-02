import type { GalleryStatus } from '../types/api/studio';
import type { GalleryCardWords } from '../types/utils/gallerySelection.types';

/** What the card of the gallery says for each state it can show the client. A gallery still waiting for its proofs is not shown. */
const WORDS: Readonly<Partial<Record<GalleryStatus, GalleryCardWords>>> = {
  InSelection: { text: 'Tus fotos de prueba ya están listas. Elige las que quieres que tu fotógrafo edite.', action: 'Ver mis fotos y elegir' },
  InEditing: { text: 'Ya enviaste tu selección. Tu fotógrafo está editando tus fotos.', action: 'Ver mi selección' },
  Ready: { text: 'Tu fotógrafo terminó de editar tus fotos. Las podrás descargar en cuanto tu saldo esté en cero.', action: 'Ver mi selección' },
  Delivered: { text: 'Tus fotos están listas para descargar.', action: 'Descargar mis fotos' },
  Archived: { text: 'Esta galería ya fue archivada.', action: null },
};

/**
 * Says where the gallery is, for the card of the client.
 * @param status State of the gallery.
 * @returns The text and the button, or null for a state that is not shown.
 */
export function describeGalleryCard(status: GalleryStatus): GalleryCardWords | null {
  return WORDS[status] ?? null;
}
