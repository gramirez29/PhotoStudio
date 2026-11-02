import { useState, type ReactElement } from 'react';
import { DOWNLOAD_PAUSE_MS } from '../constants/finals';
import { triggerDownload } from '../media/browserDownload';
import type { GalleryDownloadsProps } from '../types/components/GalleryComponents.types';
import type { PortalFinal } from '../types/api/portal';
import { downloadInSequence, toDownloads, totalBytes } from '../utils/downloads';
import { formatBytes, formatMoney } from '../utils/format';
import { CheckboxField } from './CheckboxField';
import { StepHeading } from './StepHeading';

/**
 * The gallery once it was delivered: the final photos to download, one by one or many at once. The photos of the package and the
 * extra ones are shown apart; the extras appear only when the photographer released them, and until then the page says they are
 * agreed on with the studio. Every address lasts a few minutes, so the gallery is loaded again right before a batch of downloads.
 * @param props Component props.
 * @returns The downloads.
 */
export function GalleryDownloads({ gallery, refresh, onClose }: GalleryDownloadsProps): ReactElement {
  const [marked, setMarked] = useState<ReadonlySet<string>>(new Set());
  const [started, setStarted] = useState<number | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const packageFinals = gallery.finals.filter((final) => !final.isExtra);
  const extraFinals = gallery.finals.filter((final) => final.isExtra);
  const extraChosen = gallery.selection?.extraPhotos ?? 0;
  const downloading = started !== null;
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
   * Downloads the photos one after another. The gallery is loaded again first, so the addresses are fresh whatever the time the
   * page has been open.
   * @param wanted The photos to download.
   */
  async function download(wanted: readonly PortalFinal[]): Promise<void> {
    setProblem(null);
    setStarted(0);
    const fresh = await refresh();
    if (fresh === null) {
      setProblem('No pudimos preparar la descarga. Revisa tu conexión e inténtalo de nuevo.');
      setStarted(null);
      return;
    }

    const ids = new Set(wanted.map((final) => final.id));
    await downloadInSequence(toDownloads(fresh.finals.filter((final) => ids.has(final.id))), triggerDownload, DOWNLOAD_PAUSE_MS, setStarted);
    setStarted(null);
  }

  return (
    <section className="card gallery" aria-labelledby="gallery-downloads-title">
      <StepHeading>Tus fotos están listas</StepHeading>
      <p role="status">Gracias por confiar en nosotros. Aquí puedes descargar tus fotografías, una por una o varias a la vez.</p>

      {gallery.finals.length > 0 && (
        <div className="stack stack--tight downloads__bar">
          <CheckboxField id="download-all" checked={allMarked} disabled={downloading} onChange={toggleAll}>
            Seleccionar todas ({gallery.finals.length}, {formatBytes(totalBytes(gallery.finals))})
          </CheckboxField>
          <button type="button" className="button" disabled={downloading || markedFinals.length === 0} onClick={() => void download(markedFinals)}>
            {downloading
              ? `Descargando ${started} de ${markedFinals.length}…`
              : `Descargar seleccionadas (${markedFinals.length}${markedFinals.length > 0 ? `, ${formatBytes(totalBytes(markedFinals))}` : ''})`}
          </button>
          <p className="field__hint">
            El navegador puede pedirte permiso para descargar varios archivos. Si prefieres, descárgalas una por una. Con una conexión lenta
            puede tardar.
          </p>
          {problem !== null && (
            <p className="field__error" role="alert">
              {problem}
            </p>
          )}
        </div>
      )}

      <DownloadGroup title="Fotos de tu paquete" finals={packageFinals} marked={marked} disabled={downloading} onToggle={toggle} onDownload={(final) => void download([final])} />

      {extraChosen > 0 && extraFinals.length === 0 && (
        <p className="notice" role="status">
          Elegiste {extraChosen} {extraChosen === 1 ? 'foto adicional' : 'fotos adicionales'} ({formatMoney(gallery.selection?.extraTotal ?? { amount: 0, currency: 'CRC' })}).
          PhotoSense Studio te contactará para coordinar el pago y entregártelas.
        </p>
      )}
      <DownloadGroup title="Fotos adicionales" finals={extraFinals} marked={marked} disabled={downloading} onToggle={toggle} onDownload={(final) => void download([final])} />

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
  /** True while a batch is downloading. */
  readonly disabled: boolean;
  /** Called to mark or unmark a photo. */
  readonly onToggle: (id: string) => void;
  /** Called to download one photo. */
  readonly onDownload: (final: PortalFinal) => void;
}

/**
 * One group of the photos to download: a row for each, with its mark, its name and size, and its own download button.
 * @param props Component props.
 * @returns The group, or nothing when it has no photos.
 */
function DownloadGroup({ title, finals, marked, disabled, onToggle, onDownload }: DownloadGroupProps): ReactElement | null {
  if (finals.length === 0) {
    return null;
  }

  return (
    <div className="stack stack--tight" role="group" aria-label={title}>
      <h3>
        {title} ({finals.length})
      </h3>
      <ul className="downloads__list">
        {finals.map((final) => (
          <li key={final.id} className="downloads__item">
            <CheckboxField id={`download-${final.id}`} checked={marked.has(final.id)} disabled={disabled} onChange={() => onToggle(final.id)}>
              {final.fileName} ({formatBytes(final.bytes)})
            </CheckboxField>
            <button type="button" className="button button--secondary" disabled={disabled} aria-label={`Descargar ${final.fileName}`} onClick={() => onDownload(final)}>
              Descargar
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
