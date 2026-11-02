import { FINAL_MAX_BYTES } from '../constants/upload';
import type { SelectedPhoto } from '../types/api/gallery';
import type { FinalMatch, FinalSelection, FinalSkipReason, PickedFile } from '../types/utils/upload.types';
import { isJpegFile } from './uploadFiles';

/** Characters that may separate the name of the original file from what an editing program adds to it, as in `IMG_0001-Edit`. */
const SEPARATOR = /[-_. ]/;

/**
 * Takes the extension off a file name and lowercases it, so `IMG_0001.CR2` and `img_0001.jpg` are the same photo.
 * @param name Name of a file.
 * @returns The name to compare.
 */
export function baseName(name: string): string {
  const dot = name.lastIndexOf('.');
  return (dot > 0 ? name.slice(0, dot) : name).trim().toLowerCase();
}

/**
 * Finds the photo of the selection a file is the final of. The name must be the same as the original's, ignoring the extension, or
 * start with it followed by a separator (what Lightroom adds when it exports, such as `-Edit` or `_2`). An exact name wins over one that
 * only starts the same, so `IMG_0001` never takes the file of `IMG_00010`.
 * @param file The file the photographer chose.
 * @param photos The photos the client chose.
 * @returns The photo, or null when none matches (or when the name fits more than one photo, which cannot be decided).
 */
function findPhoto(file: PickedFile, photos: readonly SelectedPhoto[]): SelectedPhoto | null {
  const name = baseName(file.name);
  const exact = photos.filter((photo) => baseName(photo.name) === name);
  if (exact.length > 0) {
    return exact.length === 1 ? (exact[0] ?? null) : null;
  }

  const prefixed = photos.filter((photo) => {
    const original = baseName(photo.name);
    return name.startsWith(original) && SEPARATOR.test(name.charAt(original.length));
  });
  return prefixed.length === 1 ? (prefixed[0] ?? null) : null;
}

/**
 * Sorts the files a photographer chose as finals: each is paired with the photo of the selection it belongs to by its name, and the
 * ones that cannot go up are left out with the reason. Two files for the same photo are not guessed between: the first is kept and
 * the other is reported as a duplicate. The order the files were chosen in is kept.
 * @param files The files chosen.
 * @param photos The photos the client chose.
 * @returns The files paired with their photo and the files left out.
 */
export function matchFinals(files: readonly PickedFile[], photos: readonly SelectedPhoto[]): FinalSelection {
  const matches: FinalMatch[] = [];
  const skipped: { file: PickedFile; reason: FinalSkipReason }[] = [];
  const taken = new Set<string>();

  for (const file of files) {
    if (!isJpegFile(file)) {
      skipped.push({ file, reason: 'not_jpeg' });
    } else if (file.bytes < 1) {
      skipped.push({ file, reason: 'empty' });
    } else if (file.bytes > FINAL_MAX_BYTES) {
      skipped.push({ file, reason: 'too_big' });
    } else {
      const photo = findPhoto(file, photos);
      if (photo === null) {
        skipped.push({ file, reason: 'no_match' });
      } else if (taken.has(photo.id)) {
        skipped.push({ file, reason: 'duplicate' });
      } else {
        taken.add(photo.id);
        matches.push({ file, photoId: photo.id, photoName: photo.name });
      }
    }
  }

  return { matches, skipped };
}

/**
 * Says in words which files were left out of the finals, or nothing when none was.
 * @param selection What came of the files chosen.
 * @returns The message, or null when every file was taken.
 */
export function describeSkippedFinals(selection: FinalSelection): string | null {
  const count = (reason: FinalSkipReason): number => selection.skipped.filter((skip) => skip.reason === reason).length;
  const parts: string[] = [];
  const noMatch = count('no_match');
  if (noMatch > 0) {
    parts.push(`${noMatch} ${noMatch === 1 ? 'no coincide' : 'no coinciden'} con ninguna foto que eligió el cliente`);
  }

  const duplicates = count('duplicate');
  if (duplicates > 0) {
    parts.push(`${duplicates} ${duplicates === 1 ? 'repite' : 'repiten'} una foto que ya tiene archivo`);
  }

  const notJpeg = count('not_jpeg');
  if (notJpeg > 0) {
    parts.push(`${notJpeg} ${notJpeg === 1 ? 'no es JPEG' : 'no son JPEG'}`);
  }

  const big = count('too_big');
  if (big > 0) {
    parts.push(`${big} ${big === 1 ? 'pesa' : 'pesan'} más de 50 MB`);
  }

  const empty = count('empty');
  if (empty > 0) {
    parts.push(`${empty} ${empty === 1 ? 'está vacío' : 'están vacíos'}`);
  }

  return parts.length === 0 ? null : `No se tomaron: ${parts.join(', ')}.`;
}
