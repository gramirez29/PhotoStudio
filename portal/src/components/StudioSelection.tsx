import { useState, type ReactElement } from 'react';
import { useStudioSelection } from '../hooks/useStudioGallery';
import type { StudioSelectedPhoto } from '../types/api/studio';
import type { StudioSelectionProps } from '../types/components/ProofComponents.types';
import { formatBytes, formatDateTime, formatMoney } from '../utils/format';
import { describeProofRequestError } from '../utils/proofMessages';

/**
 * What the client chose, in two blocks: the photos of the package and the extra ones, so the photographer tells at a glance which
 * he must edit for the package and which are agreed on apart. Each block has the names of the original files, one per line, ready to
 * copy and paste into the search of the editing software, and shows which photos already have their final.
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
        photos={packagePhotos}
        hint="Son las que cubre el paquete contratado: se editan y se entregan con el saldo en cero."
      />
      {extraPhotos.length > 0 && (
        <SelectionBlock
          title="Fotos adicionales"
          id="selection-extras"
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
  /** The photos of the block. */
  readonly photos: readonly StudioSelectedPhoto[];
  /** What the block means, in words. */
  readonly hint: string;
  /** True for the block of the extra photos, which is styled apart. */
  readonly extra?: boolean;
}

/**
 * One block of the selection: its photos, a button that copies their names and whether each already has its final.
 * @param props Component props.
 * @returns The block.
 */
function SelectionBlock({ title, id, photos, hint, extra = false }: SelectionBlockProps): ReactElement {
  const [copied, setCopied] = useState<'done' | 'failed' | null>(null);
  const withFinal = photos.filter((photo) => photo.finalBytes !== null).length;

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
      <ol className="studio-selection__names">
        {photos.map((photo) => (
          <li key={photo.id}>
            {photo.name}
            {photo.finalBytes !== null && <span className="muted"> · final subido ({formatBytes(photo.finalBytes)})</span>}
          </li>
        ))}
      </ol>
    </div>
  );
}
