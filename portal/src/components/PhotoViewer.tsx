import { useEffect, useRef, useState, type ReactElement } from 'react';
import type { PhotoViewerProps } from '../types/components/PhotoViewer.types';

/** What the viewer is showing of the photo that is current. */
interface Shown {
  /** Photo it is about. */
  readonly photoId: string;
  /** The real photo, once its address arrived; null while it is asked for or when it could not be had. */
  readonly source: string | null;
  /** True when the real photo could not be had. */
  readonly failed: boolean;
}

/**
 * One photo, large, over the page, in the quality it was edited: the real file, not a reduced copy. The address of each photo is
 * asked for when it is shown, so only one photo of several megabytes is loaded at a time; while it arrives the small image is shown.
 * It is a dialog: it takes the focus when it opens, Escape closes it, and the arrow keys move between photos.
 * @param props Component props.
 * @returns The dialog, or nothing when the position is not a photo.
 */
export function PhotoViewer({ photos, index, loadSource, onMove, onClose }: PhotoViewerProps): ReactElement | null {
  const closeButton = useRef<HTMLButtonElement>(null);
  const [shown, setShown] = useState<Shown | null>(null);
  const photo = photos[index];
  const photoId = photo?.id;
  // The latest function, kept apart so a parent that gives a new one on every render does not make the viewer ask again each time.
  const load = useRef(loadSource);

  // Declared before the effect that asks, so the latest function is the one it finds.
  useEffect(() => {
    load.current = loadSource;
  });

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
      } else if (event.key === 'ArrowRight' && index < photos.length - 1) {
        onMove(index + 1);
      } else if (event.key === 'ArrowLeft' && index > 0) {
        onMove(index - 1);
      }
    }

    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [index, onClose, onMove, photos.length]);

  useEffect(() => {
    if (photoId === undefined) {
      return undefined;
    }

    let cancelled = false;
    load.current(photoId).then(
      (source) => {
        if (!cancelled) {
          setShown({ photoId, source, failed: false });
        }
      },
      () => {
        if (!cancelled) {
          setShown({ photoId, source: null, failed: true });
        }
      },
    );
    return () => {
      cancelled = true;
    };
  }, [photoId]);

  if (photo === undefined) {
    return null;
  }

  const current = shown?.photoId === photo.id ? shown : null;
  const source = current?.source ?? photo.placeholder;

  return (
    <div className="gallery-lightbox" role="dialog" aria-modal="true" aria-label={`Foto ${index + 1} de ${photos.length}: ${photo.name}`}>
      <div className="gallery-lightbox__frame">
        {source !== null && <img src={source} alt={photo.name} draggable={false} referrerPolicy="no-referrer" />}
      </div>
      <p className="gallery-lightbox__caption" role="status">
        <strong>{photo.name}</strong>
        {photo.caption.length > 0 && <span> · {photo.caption}</span>}
        {current === null && <span> · cargando la foto en su calidad real…</span>}
        {current?.failed === true && <span> · no pudimos cargar la foto en su calidad real</span>}
      </p>
      <div className="gallery-lightbox__bar">
        <button type="button" className="button button--secondary" disabled={index === 0} onClick={() => onMove(index - 1)}>
          Anterior
        </button>
        <button type="button" className="button button--secondary" disabled={index >= photos.length - 1} onClick={() => onMove(index + 1)}>
          Siguiente
        </button>
        <button ref={closeButton} type="button" className="button button--secondary" onClick={onClose}>
          Cerrar
        </button>
      </div>
    </div>
  );
}
