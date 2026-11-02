import type { ReactElement } from 'react';
import type { GalleryPhotoTileProps } from '../types/components/GalleryComponents.types';
import { blockContextMenu } from '../utils/blockContextMenu';

/**
 * One proof of the grid. Tapping the photo picks it or drops it; a second button opens it larger. The state is said in words for
 * screen readers and shown with a mark, never with color alone.
 * @param props Component props.
 * @returns The tile.
 */
export function GalleryPhotoTile({ proof, number, chosen, readOnly, onToggle, onEnlarge }: GalleryPhotoTileProps): ReactElement {
  return (
    <li className={chosen ? 'gallery-tile gallery-tile--chosen' : 'gallery-tile'} onContextMenu={blockContextMenu}>
      <button
        type="button"
        className="gallery-tile__photo"
        aria-pressed={readOnly ? undefined : chosen}
        aria-label={readOnly ? `Foto ${number}${chosen ? ', elegida' : ''}` : `Foto ${number}, ${chosen ? 'elegida' : 'sin elegir'}`}
        disabled={readOnly}
        onClick={() => onToggle(proof.id)}
      >
        <img src={proof.url} alt="" draggable={false} loading="lazy" decoding="async" referrerPolicy="no-referrer" onContextMenu={blockContextMenu} />
        {chosen && (
          <span className="gallery-tile__mark" aria-hidden="true">
            ✓
          </span>
        )}
      </button>
      <button type="button" className="gallery-tile__zoom" aria-label={`Ver la foto ${number} más grande`} onClick={() => onEnlarge(proof.id)}>
        Ampliar
      </button>
    </li>
  );
}
