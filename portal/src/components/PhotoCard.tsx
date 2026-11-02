import { useState, type ReactElement } from 'react';
import type { PhotoCardProps } from '../types/components/PhotoCard.types';
import { DownloadIcon, PhotoIcon } from './PhotoIcons';

/**
 * One photo as a card: the image on top (pressing it opens the photo large), its name and what is known about it under the image,
 * and a download icon inside the same card. A photo can be picked with a mark over the image, and carry a label in the corner. When
 * there is no image, or it fails to load, the card shows an icon in its place instead of an empty gap, and the card keeps its size
 * so the grid never jumps.
 * @param props Component props.
 * @returns The card, as an item of a list.
 */
export function PhotoCard({ image, name, details, openLabel, onOpen, badge, select, action }: PhotoCardProps): ReactElement {
  const [failed, setFailed] = useState(false);
  const shown = image !== null && !failed ? image : null;

  return (
    <li className="photo-card">
      <div className="photo-card__media">
        <button type="button" className="photo-card__open" aria-label={openLabel} onClick={onOpen}>
          {shown === null ? (
            <span className="photo-card__placeholder">
              <PhotoIcon />
            </span>
          ) : (
            <img src={shown} alt="" loading="lazy" decoding="async" referrerPolicy="no-referrer" onError={() => setFailed(true)} />
          )}
        </button>
        {badge !== undefined && <span className={badge.warning === true ? 'photo-card__badge photo-card__badge--warning' : 'photo-card__badge'}>{badge.label}</span>}
        {select !== undefined && (
          <label className="photo-card__select">
            <input type="checkbox" checked={select.checked} disabled={select.disabled === true} onChange={(event) => select.onChange(event.target.checked)} />
            <span className="visually-hidden">{select.label}</span>
          </label>
        )}
      </div>
      <div className="photo-card__footer">
        <div className="photo-card__text">
          <span className="photo-card__name" title={name}>
            {name}
          </span>
          {details.map((line, position) => (
            <span key={position} className="photo-card__details">
              {line}
            </span>
          ))}
        </div>
        {action !== undefined && (
          <button type="button" className="photo-card__action" aria-label={action.label} title={action.label} disabled={action.disabled === true} onClick={action.onClick}>
            <DownloadIcon />
          </button>
        )}
      </div>
    </li>
  );
}
