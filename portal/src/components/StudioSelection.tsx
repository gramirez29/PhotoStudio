import { useCallback, useState, type ReactElement } from 'react';
import { studioApi } from '../api/client';
import { useDownloadFinal, useStudioSelection } from '../hooks/useStudioGallery';
import type { StudioSelectedPhoto } from '../types/api/studio';
import type { StudioSelectionProps } from '../types/components/ProofComponents.types';
import { formatBytes, formatDateTime, formatDay, formatMoney } from '../utils/format';
import { describeProofRequestError } from '../utils/proofMessages';
import { PhotoCard } from './PhotoCard';
import { PhotoViewer } from './PhotoViewer';

/**
 * What the client chose, in two blocks: the photos of the package and the extra ones, so the photographer tells at a glance which
 * he must edit for the package and which are agreed on apart. Each block is a gallery: a grid with the photo of each (the thumbnail
 * of its final or, while it has none, its proof), what is known about the final, a way to download it, and the names of the original
 * files to copy and paste into the search of the editing software. Tapping a photo opens it in the quality it was edited.
 * @param props Component props.
 * @returns The section.
 */
export function StudioSelection({ gallery }: StudioSelectionProps): ReactElement {
  const selection = useStudioSelection(gallery.id, gallery.selectionSubmittedAt !== null);

  if (selection.isPending) {
    return <p className="muted">Cargando la selección del cliente…</p>;
  }

  if (selection.isError) {
    return (
      <p className="field__error" role="alert">
        {describeProofRequestError(selection.error)}
      </p>
    );
  }

  const { photos, extraTotal, submittedAt } = selection.data;
  const packagePhotos = photos.filter((photo) => !photo.isExtra);
  const extraPhotos = photos.filter((photo) => photo.isExtra);

  return (
    <section className="stack studio-selection" aria-labelledby="selection-title">
      <h3 id="selection-title">Selección del cliente</h3>
      <p>
        <strong>
          {photos.length} {photos.length === 1 ? 'foto elegida' : 'fotos elegidas'}
        </strong>
        {submittedAt !== null && <span className="muted"> · enviada el {formatDateTime(submittedAt)}</span>}
      </p>
      <SelectionBlock
        title="Fotos del paquete"
        id="selection-package"
        galleryId={gallery.id}
        photos={packagePhotos}
        hint="Son las que cubre el paquete contratado: se editan y se entregan con el saldo en cero."
      />
      {extraPhotos.length > 0 && (
        <SelectionBlock
          title="Fotos adicionales"
          id="selection-extras"
          galleryId={gallery.id}
          photos={extraPhotos}
          hint={`Fuera del paquete: ${formatMoney(extraTotal)} en total. Se acuerdan con el cliente; editalas y entrégalas solo cuando pague.`}
          extra
        />
      )}
    </section>
  );
}

/** Props of one block of the selection. */
interface SelectionBlockProps {
  /** Heading of the block. */
  readonly title: string;
  /** Identifier prefix for the accessibility links. */
  readonly id: string;
  /** Identifier of the gallery, to ask for the real photo and for its download. */
  readonly galleryId: string;
  /** The photos of the block. */
  readonly photos: readonly StudioSelectedPhoto[];
  /** What the block means, in words. */
  readonly hint: string;
  /** True for the block of the extra photos, which is styled apart. */
  readonly extra?: boolean;
}

/**
 * One block of the selection: a grid of its photos, a button that copies their names, and the viewer of the real photos.
 * @param props Component props.
 * @returns The block.
 */
function SelectionBlock({ title, id, galleryId, photos, hint, extra = false }: SelectionBlockProps): ReactElement {
  const [copied, setCopied] = useState<'done' | 'failed' | null>(null);
  const [open, setOpen] = useState<number | null>(null);
  const download = useDownloadFinal(galleryId);
  const withFinal = photos.filter((photo) => photo.finalBytes !== null).length;

  /**
   * Gets the address of the real photo. A photo without a final shows its proof, which is what there is.
   * @param photoId Identifier of the photo.
   * @returns The address.
   */
  const loadSource = useCallback(
    async (photoId: string): Promise<string> => {
      const photo = photos.find((candidate) => candidate.id === photoId);
      if (photo?.finalBytes === null || photo === undefined) {
        return photo?.thumbUrl ?? '';
      }

      return (await studioApi.getFinalDownload(galleryId, photoId)).url;
    },
    [galleryId, photos],
  );

  /** Copies the names of the files of this block, one per line. Without permission the page says so instead of failing silently. */
  async function copyNames(): Promise<void> {
    try {
      await navigator.clipboard.writeText(photos.map((photo) => photo.name).join('\n'));
      setCopied('done');
    } catch {
      setCopied('failed');
    }
  }

  return (
    <div className={extra ? 'selection-block selection-block--extra' : 'selection-block'} role="group" aria-labelledby={`${id}-title`}>
      <h4 id={`${id}-title`}>
        {title} ({photos.length})
      </h4>
      <p className="muted">{hint}</p>
      <p className="field__hint">
        {withFinal} de {photos.length} con su final
      </p>
      <div className="actions">
        <button type="button" className="button button--secondary" disabled={photos.length === 0} onClick={() => void copyNames()}>
          Copiar los nombres de los archivos
        </button>
      </div>
      <p className="visually-hidden" role="status">
        {copied === 'done' ? 'Nombres copiados.' : ''}
      </p>
      {copied === 'done' && <p className="field__hint">Copiados. Pégalos en la búsqueda de tu programa de edición.</p>}
      {copied === 'failed' && (
        <p className="field__error" role="alert">
          Tu navegador no permitió copiar. Selecciona la lista y cópiala a mano.
        </p>
      )}
      <ul className="photo-grid" aria-label={title}>
        {photos.map((photo, position) => (
          <PhotoCard
            key={photo.id}
            image={photo.thumbUrl}
            name={photo.name}
            details={describeFinalLines(photo)}
            openLabel={`Ver ${photo.name}`}
            onOpen={() => setOpen(position)}
            badge={photo.finalBytes === null ? { label: 'Sin final', warning: true } : { label: 'Final' }}
            action={photo.finalBytes === null ? undefined : { label: `Descargar el final de ${photo.name}`, onClick: () => download.mutate(photo.id), disabled: download.isPending }}
          />
        ))}
      </ul>
      {download.isError && (
        <p className="field__error" role="alert">
          {describeProofRequestError(download.error)}
        </p>
      )}
      {open !== null && (
        <PhotoViewer
          photos={photos.map((photo) => ({
            id: photo.id,
            name: photo.name,
            caption: photo.finalBytes === null ? 'prueba, todavía sin final' : describeFinal(photo),
            placeholder: photo.thumbUrl,
          }))}
          index={open}
          loadSource={loadSource}
          onMove={setOpen}
          onClose={() => setOpen(null)}
        />
      )}
    </div>
  );
}

/**
 * Describes the final of a photo in short lines for its card: the size in pixels and in megabytes, when it was taken and how many
 * times the client asked to download it; or that it has none yet.
 * @param photo The photo.
 * @returns The lines, one under the other.
 */
export function describeFinalLines(photo: StudioSelectedPhoto): readonly string[] {
  if (photo.finalBytes === null) {
    return ['Todavía sin final'];
  }

  const size = [photo.finalWidth !== null && photo.finalHeight !== null ? `${photo.finalWidth} × ${photo.finalHeight} px` : null, formatBytes(photo.finalBytes)]
    .filter((part): part is string => part !== null)
    .join(' · ');
  const lines = [size];
  if (photo.finalCapturedAt !== null) {
    lines.push(`Tomada el ${formatDay(photo.finalCapturedAt)}`);
  }

  lines.push(photo.finalDownloads === 0 ? 'Sin descargas' : `Descargada ${photo.finalDownloads} ${photo.finalDownloads === 1 ? 'vez' : 'veces'}`);
  return lines;
}

/**
 * Describes the final of a photo for the photographer: its size in pixels and in megabytes, when it was taken and how many times
 * the client asked to download it; or that it has none yet.
 * @param photo The photo.
 * @returns The text.
 */
export function describeFinal(photo: StudioSelectedPhoto): string {
  if (photo.finalBytes === null) {
    return 'sin final';
  }

  const parts = ['final subido'];
  if (photo.finalWidth !== null && photo.finalHeight !== null) {
    parts.push(`${photo.finalWidth} × ${photo.finalHeight} px`);
  }

  parts.push(formatBytes(photo.finalBytes));
  if (photo.finalCapturedAt !== null) {
    parts.push(`tomada el ${formatDay(photo.finalCapturedAt)}`);
  }

  parts.push(photo.finalDownloads === 0 ? 'sin descargas' : `descargada ${photo.finalDownloads} ${photo.finalDownloads === 1 ? 'vez' : 'veces'}`);
  return parts.join(' · ');
}
