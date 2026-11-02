import { useRef, useState, type ChangeEvent, type ReactElement } from 'react';
import { ZIP_ACCEPT } from '../constants/finals';
import { useZipUpload } from '../hooks/useZipUpload';
import { useRemoveZip } from '../hooks/useStudioGallery';
import type { StudioZipProps } from '../types/components/ProofComponents.types';
import { formatBytes, formatDateTime } from '../utils/format';
import { describeFinalsRequestError } from '../utils/proofMessages';

/**
 * The ZIP with the photos, which the photographer makes by hand: the way to upload it (up to 500 MB, straight to the storage), what
 * the client did with it (whether and when they downloaded it) and the way to delete it, for example to upload another when the
 * client asks again. The client sees a button to download it, with its size, only once the gallery is delivered.
 * @param props Component props.
 * @returns The section.
 */
export function StudioZip({ gallery }: StudioZipProps): ReactElement {
  const input = useRef<HTMLInputElement>(null);
  const upload = useZipUpload(gallery);
  const remove = useRemoveZip(gallery.id);
  const [confirming, setConfirming] = useState(false);
  const canUpload = gallery.allowedActions.includes('UploadZip');
  const canRemove = gallery.allowedActions.includes('RemoveZip');
  const busy = upload.phase !== 'idle' || remove.isPending;
  const zip = gallery.zip;

  /**
   * Hands the chosen file over and clears the input, so choosing the same file again still counts as a change.
   * @param event Change event of the input.
   */
  function handleChange(event: ChangeEvent<HTMLInputElement>): void {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file !== undefined) {
      void upload.upload(file);
    }
  }

  return (
    <section className="stack stack--tight" aria-labelledby="zip-title">
      <h3 id="zip-title">ZIP con las fotos (opcional)</h3>
      <p className="muted">
        Hazlo tú y súbelo aquí si el cliente prefiere un solo archivo: hasta 500 MB. El cliente lo ve cuando la galería está entregada, y
        se le avisa cuánto pesa antes de descargarlo. No incluyas las fotos adicionales si todavía no las pagó.
      </p>
      {zip === null ? (
        <p>Todavía no hay un ZIP.</p>
      ) : (
        <div role="status">
          <p>
            <strong>{formatBytes(zip.bytes)}</strong> · subido el {formatDateTime(zip.uploadedAt)}
          </p>
          <p>
            {zip.downloads === 0
              ? 'El cliente todavía no lo ha descargado.'
              : `El cliente lo ha descargado ${zip.downloads} ${zip.downloads === 1 ? 'vez' : 'veces'}${zip.lastDownloadedAt === null ? '' : ` (la última, el ${formatDateTime(zip.lastDownloadedAt)})`}.`}
          </p>
        </div>
      )}
      {upload.phase === 'uploading' && (
        <p role="status" aria-live="polite">
          Subiendo el ZIP… puede tardar varios minutos según tu conexión. No cierres esta página.
        </p>
      )}
      {upload.phase === 'registering' && <p role="status">Confirmando el ZIP…</p>}
      {(upload.problem !== null || remove.isError) && (
        <p className="field__error" role="alert">
          {upload.problem ?? describeFinalsRequestError(remove.error)}
        </p>
      )}
      <input ref={input} type="file" accept={ZIP_ACCEPT} hidden aria-label="Elegir el ZIP" onChange={handleChange} />
      <div className="actions">
        {canUpload && (
          <button type="button" className="button" disabled={busy} onClick={() => input.current?.click()}>
            {zip === null ? 'Elegir el ZIP' : 'Subir otro ZIP'}
          </button>
        )}
        {canRemove && !confirming && (
          <button type="button" className="button button--secondary" disabled={busy} onClick={() => setConfirming(true)}>
            Borrar el ZIP
          </button>
        )}
      </div>
      {canRemove && confirming && (
        <div className="stack stack--tight">
          <p>El cliente ya no podrá descargarlo hasta que subas uno nuevo. Las fotos no se borran.</p>
          <div className="actions">
            <button
              type="button"
              className="button"
              disabled={remove.isPending}
              onClick={() => remove.mutate(undefined, { onSettled: () => setConfirming(false) })}
            >
              {remove.isPending ? 'Borrando…' : 'Sí, borrar el ZIP'}
            </button>
            <button type="button" className="button button--secondary" disabled={remove.isPending} onClick={() => setConfirming(false)}>
              Cancelar
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
