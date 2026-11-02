import type { GalleryResponse, GalleryStatus, SelectedPhoto, SelectionResponse } from '../types/api/gallery';
import type { GalleryStatusView } from '../types/utils/gallery.types';
import { formatBytes, formatDate, formatDateTime, formatMegabytes, formatMoney, formatShortDate } from './format';

/** What each state of a gallery says to the photographer, and the color it takes. */
const STATUS_VIEWS: Readonly<Record<GalleryStatus, GalleryStatusView>> = {
  Pending: { label: 'Esperando las pruebas', hint: 'Sube las pruebas y publícalas para que el cliente elija.', tone: 'warning' },
  InSelection: { label: 'El cliente está eligiendo', hint: 'El cliente ya puede elegir sus fotos desde su enlace.', tone: 'warning' },
  InEditing: { label: 'En edición', hint: 'El cliente envió su selección: edita esas fotos.', tone: 'info' },
  Ready: { label: 'Fotos listas', hint: 'Publicadas. El cliente las descarga en cuanto su saldo esté en cero.', tone: 'success' },
  Delivered: { label: 'Entregada', hint: 'El cliente ya puede descargar sus fotos.', tone: 'success' },
  Archived: { label: 'Archivada', hint: 'Las fotos ya no están disponibles.', tone: 'muted' },
};

/**
 * Says how long ago a gallery was delivered, for the list of galleries whose space can be freed, for example
 * "Entregada el 17/10/2026 · hace 12 días". Without a delivery date it says nothing about the time.
 * @param gallery The gallery.
 * @param now Current instant in milliseconds since the epoch.
 * @returns The line.
 */
export function describeDeliveryAge(gallery: GalleryResponse, now: number): string {
  if (gallery.deliveredAt === null) {
    return 'Entregada';
  }

  const days = Math.max(0, Math.floor((now - Date.parse(gallery.deliveredAt)) / 86_400_000));
  const ago = days === 0 ? 'hoy' : days === 1 ? 'hace 1 día' : `hace ${days} días`;
  return `Entregada el ${formatShortDate(gallery.deliveredAt)} · ${ago}`;
}

/**
 * Says where a gallery is, in words and with a color.
 * @param status State of the gallery.
 * @returns What to show.
 */
export function describeGalleryStatus(status: GalleryStatus): GalleryStatusView {
  return STATUS_VIEWS[status];
}

/**
 * Writes the terms of a gallery, for example "30 fotos incluidas · 5 de regalo · ₡3 000 cada foto extra".
 * @param gallery The gallery.
 * @returns The text; the gift is mentioned only when there is one.
 */
export function describeTerms(gallery: GalleryResponse): string {
  const included = `${gallery.includedPhotos} ${gallery.includedPhotos === 1 ? 'foto incluida' : 'fotos incluidas'}`;
  const gift = gallery.bonusPhotos > 0 ? ` · ${gallery.bonusPhotos} de regalo` : '';
  return `${included}${gift} · ${formatMoney(gallery.extraPhotoPrice)} cada foto extra`;
}

/**
 * Describes the final of a photo for the photographer: its size in pixels and in megabytes, when it was taken and how many times
 * the client asked to download it; or that it has none yet.
 * @param photo The photo.
 * @returns The text.
 */
export function describeFinal(photo: SelectedPhoto): string {
  if (photo.finalBytes === null) {
    return 'sin final';
  }

  const parts = ['final subido'];
  if (photo.finalWidth !== null && photo.finalHeight !== null) {
    parts.push(`${photo.finalWidth} × ${photo.finalHeight} px`);
  }

  parts.push(formatBytes(photo.finalBytes));
  if (photo.finalCapturedAt !== null) {
    parts.push(`tomada el ${formatDate(new Date(photo.finalCapturedAt))}`);
  }

  parts.push(photo.finalDownloads === 0 ? 'sin descargas' : `descargada ${photo.finalDownloads} ${photo.finalDownloads === 1 ? 'vez' : 'veces'}`);
  return parts.join(' · ');
}

/**
 * Describes the final of a photo in four short lines for its card, each with its own value: the size in pixels, the weight, the date
 * it was taken and how many times the client downloaded it. A value that is not known is shown as a dash, so the four lines are
 * always there and the cards line up.
 * @param photo The photo.
 * @returns The four lines, one under the other.
 */
export function describeFinalLines(photo: SelectedPhoto): readonly string[] {
  const hasFinal = photo.finalBytes !== null;
  return [
    photo.finalWidth !== null && photo.finalHeight !== null ? `${photo.finalWidth} x ${photo.finalHeight} px` : '-',
    `Tamaño: ${photo.finalBytes === null ? '-' : formatMegabytes(photo.finalBytes)}`,
    `Tomada: ${photo.finalCapturedAt === null ? '-' : formatShortDate(photo.finalCapturedAt)}`,
    `Descargas hechas: ${hasFinal ? photo.finalDownloads : '-'}`,
  ];
}

/**
 * Splits what the client chose in the photos the package covers and the extra ones.
 * @param selection What the client chose.
 * @returns The two groups.
 */
export function splitSelection(selection: SelectionResponse): { readonly packagePhotos: readonly SelectedPhoto[]; readonly extraPhotos: readonly SelectedPhoto[] } {
  return { packagePhotos: selection.photos.filter((photo) => !photo.isExtra), extraPhotos: selection.photos.filter((photo) => photo.isExtra) };
}

/**
 * Joins the names of the original files, one per line, to paste into the search of the editing software.
 * @param photos The photos.
 * @returns The names.
 */
export function namesOf(photos: readonly SelectedPhoto[]): string {
  return photos.map((photo) => photo.name).join('\n');
}

/**
 * Describes the ZIP the photographer uploaded and what the client did with it.
 * @param zip The ZIP.
 * @param zip.bytes Size in bytes.
 * @param zip.uploadedAt Instant it was registered (ISO 8601).
 * @param zip.downloads How many times the client asked for it.
 * @param zip.lastDownloadedAt Last instant the client asked for it (ISO 8601), or null.
 * @returns Two lines: what it is and whether the client downloaded it.
 */
export function describeZip(zip: { readonly bytes: number; readonly uploadedAt: string; readonly downloads: number; readonly lastDownloadedAt: string | null }): readonly string[] {
  const downloaded =
    zip.downloads === 0
      ? 'El cliente todavía no lo ha descargado.'
      : `El cliente lo ha descargado ${zip.downloads} ${zip.downloads === 1 ? 'vez' : 'veces'}${zip.lastDownloadedAt === null ? '' : ` (la última, el ${formatDateTime(zip.lastDownloadedAt)})`}.`;
  return [`${formatBytes(zip.bytes)} · subido el ${formatDateTime(zip.uploadedAt)}`, downloaded];
}

/**
 * Validates how many photos the package includes and how many are a gift, as typed.
 * @param included Included photos, as typed.
 * @param bonus Gift photos, as typed.
 * @param price Price of an extra photo, as typed.
 * @param maxPhotos Most photos a gallery holds.
 * @returns The numbers, or what is wrong with them.
 */
export function validateTerms(
  included: string,
  bonus: string,
  price: string,
  maxPhotos: number,
): { readonly ok: true; readonly includedPhotos: number; readonly bonusPhotos: number; readonly extraPhotoPrice: number } | { readonly ok: false; readonly error: string } {
  const toCount = (text: string): number => (/^\d{1,3}$/.test(text.trim()) ? Number(text.trim()) : Number.NaN);
  const includedPhotos = toCount(included);
  const bonusPhotos = toCount(bonus.trim().length === 0 ? '0' : bonus);
  const extraPhotoPrice = /^\d{1,7}$/.test(price.replace(/[\s.,]/g, '')) ? Number(price.replace(/[\s.,]/g, '')) : Number.NaN;
  if (Number.isNaN(includedPhotos) || Number.isNaN(bonusPhotos)) {
    return { ok: false, error: 'Escribe las fotos incluidas y las de regalo con números enteros, sin signos.' };
  }

  if (Number.isNaN(extraPhotoPrice)) {
    return { ok: false, error: 'Escribe el precio de cada foto extra en colones, solo con números.' };
  }

  if (includedPhotos + bonusPhotos > maxPhotos) {
    return { ok: false, error: `Una galería tiene como máximo ${maxPhotos} fotos entre incluidas y de regalo.` };
  }

  return { ok: true, includedPhotos, bonusPhotos, extraPhotoPrice };
}

/**
 * Validates the reason to reopen the selection.
 * @param text The reason, as typed.
 * @param maxLength Longest reason accepted.
 * @returns The reason without spaces around it, or what is wrong.
 */
export function validateReopenReason(text: string, maxLength: number): { readonly ok: true; readonly reason: string } | { readonly ok: false; readonly error: string } {
  const reason = text.trim();
  if (reason.length === 0) {
    return { ok: false, error: 'Escribe el motivo: queda en tu registro y el cliente no lo ve.' };
  }

  return reason.length > maxLength ? { ok: false, error: `El motivo no puede tener más de ${maxLength} caracteres.` } : { ok: true, reason };
}
