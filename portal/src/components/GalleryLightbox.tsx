import { useEffect, useRef, type ReactElement } from 'react';
import type { GalleryLightboxProps } from '../types/components/GalleryComponents.types';
import { blockContextMenu } from '../utils/blockContextMenu';

/**
 * One proof, large, over the page, with the way to pick it and to go to the next or the previous one. It is a dialog: it takes the
 * focus when it opens, Escape closes it, and the arrow keys move between photos.
 * @param props Component props.
 * @returns The dialog, or nothing when the position is not a photo.
 */
export function GalleryLightbox({ proofs, index, chosen, readOnly, onToggle, onMove, onClose }: GalleryLightboxProps): ReactElement | null {
  const closeButton = useRef<HTMLButtonElement>(null);
  const proof = proofs[index];

  useEffect(() => {
    closeButton.current?.focus();
  }, []);

  useEffect(() => {
    /**
     * Handles the keys of the dialog.
     * @param event Keyboard event of the page.
     */
    function onKey(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onClose();
      } else if (event.key === 'ArrowRight' && index < proofs.length - 1) {
        onMove(index + 1);
      } else if (event.key === 'ArrowLeft' && index > 0) {
        onMove(index - 1);
      }
    }

    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [index, onClose, onMove, proofs.length]);

  if (proof === undefined) {
    return null;
  }

  const isChosen = chosen.includes(proof.id);

  return (
    <div className="gallery-lightbox" role="dialog" aria-modal="true" aria-label={`Foto ${index + 1} de ${proofs.length}`}>
      <div className="gallery-lightbox__frame" onContextMenu={blockContextMenu}>
        <img src={proof.url} alt={`Foto ${index + 1}`} draggable={false} referrerPolicy="no-referrer" onContextMenu={blockContextMenu} />
      </div>
      <div className="gallery-lightbox__bar">
        <button type="button" className="button button--secondary" disabled={index === 0} onClick={() => onMove(index - 1)}>
          Anterior
        </button>
        {!readOnly && (
          <button type="button" className="button" aria-pressed={isChosen} onClick={() => onToggle(proof.id)}>
            {isChosen ? 'Quitar de mi selección' : 'Elegir esta foto'}
          </button>
        )}
        <button type="button" className="button button--secondary" disabled={index >= proofs.length - 1} onClick={() => onMove(index + 1)}>
          Siguiente
        </button>
        <button ref={closeButton} type="button" className="button button--secondary" onClick={onClose}>
          Cerrar
        </button>
      </div>
    </div>
  );
}
