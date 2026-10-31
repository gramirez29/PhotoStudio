import { useState, type ReactElement } from 'react';
import { useStudioSelection } from '../hooks/useStudioGallery';
import type { StudioSelectionProps } from '../types/components/ProofComponents.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { describeProofRequestError } from '../utils/proofMessages';

/**
 * What the client chose: how many photos, which are extra and what they cost, and the names of the original files, one per line,
 * ready to copy and paste into the search of the editing software.
 * @param props Component props.
 * @returns The section.
 */
export function StudioSelection({ gallery }: StudioSelectionProps): ReactElement {
  const selection = useStudioSelection(gallery.id, gallery.selectionSubmittedAt !== null);
  const [copied, setCopied] = useState<'done' | 'failed' | null>(null);

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

  const { photos, extraPhotos, extraTotal, submittedAt } = selection.data;

  /** Copies the names of the files, one per line. Without permission the page says so instead of failing silently. */
  async function copyNames(): Promise<void> {
    try {
      await navigator.clipboard.writeText(photos.map((photo) => photo.name).join('\n'));
      setCopied('done');
    } catch {
      setCopied('failed');
    }
  }

  return (
    <section className="stack stack--tight studio-selection" aria-labelledby="selection-title">
      <h3 id="selection-title">Selección del cliente</h3>
      <p>
        <strong>
          {photos.length} {photos.length === 1 ? 'foto elegida' : 'fotos elegidas'}
        </strong>
        {submittedAt !== null && <span className="muted"> · enviada el {formatDateTime(submittedAt)}</span>}
      </p>
      {extraPhotos > 0 && (
        <p>
          {extraPhotos} {extraPhotos === 1 ? 'es adicional' : 'son adicionales'}: {formatMoney(extraTotal)}.
        </p>
      )}
      <div className="actions">
        <button type="button" className="button button--secondary" onClick={() => void copyNames()}>
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
          <li key={photo.id}>{photo.name}</li>
        ))}
      </ol>
    </section>
  );
}
