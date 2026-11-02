import { useState, type ReactElement } from 'react';
import { DOWNLOAD_PAUSE_MS } from '../constants/finals';
import { triggerDownload } from '../media/browserDownload';
import type { PortalFinal } from '../types/api/portal';
import type { GalleryDownloadsProps } from '../types/components/GalleryComponents.types';
import { describeDownloadError } from '../utils/apiErrors';
import { downloadInSequence, toDownloads, totalBytes } from '../utils/downloads';
import { formatBytes, formatMoney } from '../utils/format';
import { CheckboxField } from './CheckboxField';
import { PhotoViewer } from './PhotoViewer';
import { StepHeading } from './StepHeading';

/**
 * The gallery once it was delivered: the final photos to download, one by one or many at once, and the ZIP with all of them when
 * the photographer made one. The photos of the package and the extra ones are shown apart; the extras appear only when the
 * photographer released them. The addresses are asked for at the moment of downloading (they last a few minutes, and asking counts as
 * a download, so the photographer knows what the client took). The ZIP asks the client to confirm first, because of its size.
 * @param props Component props.
 * @returns The downloads.
 */
export function GalleryDownloads({ gallery, requestDownloads, requestView, requestZip, onSessionExpired, onClose }: GalleryDownloadsProps): ReactElement {
  const [marked, setMarked] = useState<ReadonlySet<string>>(new Set());
  const [started, setStarted] = useState<number | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [confirmingZip, setConfirmingZip] = useState(false);
  const [viewing, setViewing] = useState<string | null>(null);
  const [zipBusy, setZipBusy] = useState(false);
  const packageFinals = gallery.finals.filter((final) => !final.isExtra);
  const extraFinals = gallery.finals.filter((final) => final.isExtra);
  const extraChosen = gallery.selection?.extraPhotos ?? 0;
  const busy = started !== null || zipBusy;
  const allMarked = gallery.finals.length > 0 && marked.size === gallery.finals.length;
  const markedFinals = gallery.finals.filter((final) => marked.has(final.id));

  /**
   * Marks or unmarks one photo.
   * @param id Identifier of the photo.
   */
  function toggle(id: string): void {
    setMarked((current) => {
      const next = new Set(current);
      if (!next.delete(id)) {
        next.add(id);
      }

      return next;
    });
  }

  /** Marks every photo, or none when every photo was marked. */
  function toggleAll(): void {
    setMarked(allMarked ? new Set() : new Set(gallery.finals.map((final) => final.id)));
  }

  /**
   * Shows why something failed, or sends the client back to the code when the verification expired.
   * @param error What the request threw.
   */
  function fail(error: unknown): void {
    const view = describeDownloadError(error);
    if (view.sessionExpired) {
      onSessionExpired();
    } else {
      setProblem(view.message);
    }
  }

  /**
   * Downloads the photos one after another. The addresses are asked for right now, so they are fresh whatever the time the page has
   * been open, and the photographer sees the downloads.
   * @param wanted The photos to download.
   */
  async function download(wanted: readonly PortalFinal[]): Promise<void> {
    setProblem(null);
    setStarted(0);
    try {
      const links = await requestDownloads(wanted.map((final) => final.id));
      await downloadInSequence(toDownloads(links), triggerDownload, DOWNLOAD_PAUSE_MS, setStarted);
    } catch (error) {
      fail(error);
    } finally {
      setStarted(null);
    }
  }

  /** Downloads the ZIP, after the client confirmed it. */
  async function downloadZip(): Promise<void> {
    setProblem(null);
    setZipBusy(true);
    try {
      const link = await requestZip();
      triggerDownload({ url: link.url, fileName: link.fileName });
      setConfirmingZip(false);
    } catch (error) {
      fail(error);
    } finally {
      setZipBusy(false);
    }
  }

  return (
    <section className="card gallery" aria-labelledby="gallery-downloads-title">
      <StepHeading>Tus fotos están listas</StepHeading>
      <p role="status">Gracias por confiar en nosotros. Aquí puedes descargar tus fotografías, una por una o varias a la vez.</p>

      {gallery.zip !== null && (
        <div className="stack stack--tight downloads__zip" role="group" aria-label="Descargar todo en un ZIP">
          <h3>Todas en un solo archivo</h3>
          {confirmingZip ? (
            <>
              <p role="alert">
                Se va a descargar un paquete de <strong>{formatBytes(gallery.zip.bytes)}</strong>. Según tu conexión a internet puede tomar tiempo.
              </p>
              <div className="actions">
                <button type="button" className="button" disabled={busy} onClick={() => void downloadZip()}>
                  {zipBusy ? 'Preparando…' : 'Sí, descargar el ZIP'}
                </button>
                <button type="button" className="button button--secondary" disabled={zipBusy} onClick={() => setConfirmingZip(false)}>
                  Cancelar
                </button>
              </div>
            </>
          ) : (
            <button type="button" className="button button--secondary" disabled={busy} onClick={() => setConfirmingZip(true)}>
              Descargar todo en un ZIP ({formatBytes(gallery.zip.bytes)})
            </button>
          )}
        </div>
      )}

      {gallery.finals.length > 0 && (
        <div className="stack stack--tight downloads__bar">
          <CheckboxField id="download-all" checked={allMarked} disabled={busy} onChange={toggleAll}>
            Seleccionar todas ({gallery.finals.length}, {formatBytes(totalBytes(gallery.finals))})
          </CheckboxField>
          <button type="button" className="button" disabled={busy || markedFinals.length === 0} onClick={() => void download(markedFinals)}>
            {started !== null
              ? `Descargando ${started} de ${markedFinals.length}…`
              : `Descargar seleccionadas (${markedFinals.length}${markedFinals.length > 0 ? `, ${formatBytes(totalBytes(markedFinals))}` : ''})`}
          </button>
          <p className="field__hint">
            El navegador puede pedirte permiso para descargar varios archivos. Si prefieres, descárgalas una por una. Con una conexión lenta
            puede tardar.
          </p>
        </div>
      )}
      {problem !== null && (
        <p className="field__error" role="alert">
          {problem}
        </p>
      )}

      <DownloadGroup title="Fotos de tu paquete" finals={packageFinals} marked={marked} disabled={busy} onToggle={toggle} onDownload={(final) => void download([final])} onView={(final) => setViewing(final.id)} />

      {extraChosen > 0 && extraFinals.length === 0 && (
        <p className="notice" role="status">
          Elegiste {extraChosen} {extraChosen === 1 ? 'foto adicional' : 'fotos adicionales'} ({formatMoney(gallery.selection?.extraTotal ?? { amount: 0, currency: 'CRC' })}).
          PhotoSense Studio te contactará para coordinar el pago y entregártelas.
        </p>
      )}
      <DownloadGroup title="Fotos adicionales" finals={extraFinals} marked={marked} disabled={busy} onToggle={toggle} onDownload={(final) => void download([final])} onView={(final) => setViewing(final.id)} />
      {viewing !== null && (
        <PhotoViewer
          photos={gallery.finals.map((final) => ({ id: final.id, name: final.fileName, caption: formatBytes(final.bytes), placeholder: final.thumbUrl }))}
          index={Math.max(0, gallery.finals.findIndex((final) => final.id === viewing))}
          loadSource={requestView}
          onMove={(position) => setViewing(gallery.finals[position]?.id ?? null)}
          onClose={() => setViewing(null)}
        />
      )}

      <div className="actions">
        <button type="button" className="button button--secondary" onClick={onClose}>
          Cerrar
        </button>
      </div>
    </section>
  );
}

/** Props of one group of downloadable photos. */
interface DownloadGroupProps {
  /** Heading of the group. */
  readonly title: string;
  /** The finals of the group. */
  readonly finals: readonly PortalFinal[];
  /** Identifiers of the photos that are marked. */
  readonly marked: ReadonlySet<string>;
  /** True while something is downloading. */
  readonly disabled: boolean;
  /** Called to mark or unmark a photo. */
  readonly onToggle: (id: string) => void;
  /** Called to download one photo. */
  readonly onDownload: (final: PortalFinal) => void;
  /** Called to look at one photo in its real quality. */
  readonly onView: (final: PortalFinal) => void;
}

/**
 * One group of the photos to download: a row for each, with its mark, its name and size, and its own download button.
 * @param props Component props.
 * @returns The group, or nothing when it has no photos.
 */
function DownloadGroup({ title, finals, marked, disabled, onToggle, onDownload, onView }: DownloadGroupProps): ReactElement | null {
  if (finals.length === 0) {
    return null;
  }

  return (
    <div className="stack stack--tight" role="group" aria-label={title}>
      <h3>
        {title} ({finals.length})
      </h3>
      <ul className="final-grid">
        {finals.map((final) => (
          <li key={final.id} className="final-tile">
            <button type="button" className="final-tile__photo" aria-label={`Ver ${final.fileName}`} onClick={() => onView(final)}>
              {final.thumbUrl !== null && <img src={final.thumbUrl} alt="" loading="lazy" decoding="async" referrerPolicy="no-referrer" />}
            </button>
            <CheckboxField id={`download-${final.id}`} checked={marked.has(final.id)} disabled={disabled} onChange={() => onToggle(final.id)}>
              {final.fileName} ({formatBytes(final.bytes)})
            </CheckboxField>
            <button type="button" className="button button--secondary final-tile__action" disabled={disabled} aria-label={`Descargar ${final.fileName}`} onClick={() => onDownload(final)}>
              Descargar
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
