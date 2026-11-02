import { useState, type ReactElement } from 'react';
import { useReopenSelection } from '../hooks/useStudioGallery';
import type { ReopenSelectionPanelProps } from '../types/components/ProofComponents.types';
import { describeFinalsRequestError } from '../utils/proofMessages';
import { TextField } from './TextField';

/** Longest reason the API accepts. */
const REASON_MAX_LENGTH = 500;

/**
 * The step that lets the client choose again, for example because they asked to change some photos. It asks for the reason (kept
 * for the photographer's record, never sent to the client) and for confirmation, because what the client chose is discarded and the
 * client is told by email. The finals already uploaded stay tied to their photo.
 * @param props Component props.
 * @returns The panel.
 */
export function ReopenSelectionPanel({ gallery, disabled }: ReopenSelectionPanelProps): ReactElement {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const reopen = useReopenSelection(gallery.id);
  const trimmed = reason.trim();

  return (
    <section className="stack proof-publish" aria-labelledby="reopen-title">
      <h3 id="reopen-title">Reabrir la selección</h3>
      <p className="muted">
        El cliente podrá elegir de nuevo y recibirá un correo. Lo que eligió se descarta; los finales que ya subiste se conservan para cada foto.
        Solo se puede antes de publicar los finales.
      </p>
      {reopen.isError && (
        <p className="field__error" role="alert">
          {describeFinalsRequestError(reopen.error)}
        </p>
      )}
      {open ? (
        <div className="stack stack--tight">
          <TextField id="reopen-reason" label="Motivo (solo lo ves tú)" value={reason} onChange={setReason} maxLength={REASON_MAX_LENGTH} autoFocus />
          <div className="actions">
            <button type="button" className="button" disabled={reopen.isPending || trimmed.length === 0} onClick={() => reopen.mutate(trimmed)}>
              {reopen.isPending ? 'Reabriendo…' : 'Sí, reabrir la selección'}
            </button>
            <button type="button" className="button button--secondary" disabled={reopen.isPending} onClick={() => setOpen(false)}>
              Todavía no
            </button>
          </div>
        </div>
      ) : (
        <button type="button" className="button button--secondary" disabled={disabled} onClick={() => setOpen(true)}>
          Reabrir la selección
        </button>
      )}
    </section>
  );
}
