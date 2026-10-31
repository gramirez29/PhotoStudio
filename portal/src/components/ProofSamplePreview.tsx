import type { ReactElement } from 'react';
import { useProofSample } from '../hooks/useProofSample';
import type { ProofSamplePreviewProps } from '../types/components/ProofComponents.types';

/**
 * Shows how the proofs will look, made from the first of the photos chosen, and asks the photographer to say go. The proofs go to
 * the client with this size, this quality and this watermark, so it is better to see them once before sending a hundred.
 * @param props Component props.
 * @returns The preview.
 */
export function ProofSamplePreview({ files, onConfirm, onCancel }: ProofSamplePreviewProps): ReactElement {
  const sample = useProofSample(files[0]);
  const count = files.length;

  return (
    <section className="stack proof-sample" aria-labelledby="proof-sample-title">
      <h3 id="proof-sample-title">Así verá tu cliente las pruebas</h3>
      <div className="proof-sample__frame" role="status" aria-live="polite">
        {sample.state === 'loading' && <p className="muted">Preparando la muestra…</p>}
        {sample.state === 'failed' && <p className="field__error">No se pudo preparar la muestra con la primera foto. Puedes seguir de todos modos: las que no se puedan preparar se marcarán.</p>}
        {sample.url !== null && <img src={sample.url} alt="Muestra de una prueba, con la marca de agua en el centro" />}
      </div>
      <p className="muted">
        Son pruebas en calidad baja y con la marca de agua de PhotoSense Studio. Se preparan aquí, en tu computadora: las fotos originales no se suben.
      </p>
      <div className="actions">
        <button type="button" className="button" onClick={onConfirm}>
          Preparar y subir {count} {count === 1 ? 'foto' : 'fotos'}
        </button>
        <button type="button" className="button button--secondary" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </section>
  );
}
