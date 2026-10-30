import type { ReactElement } from 'react';
import type { StudioGalleryCardProps } from '../types/components/StudioComponents.types';
import { formatDateTime } from '../utils/format';
import { describeExtraPhoto, describeGalleryStatus, describePhotos } from '../utils/studio';
import { StatusBadge } from './StatusBadge';

/**
 * One gallery of the list: who it is for, which session it comes from, where it is, and the terms of its photos. It shows facts the
 * API already decided; the page never works out by itself what a state allows.
 * @param props Component props.
 * @returns The card.
 */
export function StudioGalleryCard({ gallery }: StudioGalleryCardProps): ReactElement {
  const status = describeGalleryStatus(gallery.status);

  return (
    <article className="card gallery-card" aria-labelledby={`gallery-${gallery.id}`}>
      <div className="gallery-card__head">
        <h3 id={`gallery-${gallery.id}`}>{gallery.clientName}</h3>
        <StatusBadge label={status.label} tone={status.tone} />
      </div>
      <p>{gallery.packageName}</p>
      <p className="muted">{formatDateTime(gallery.sessionStart)}</p>
      <p>{describePhotos(gallery)}</p>
      <p className="muted">{describeExtraPhoto(gallery)}</p>
    </article>
  );
}
