import type { PickedFile, ProofSelection } from '../types/utils/upload.types';

/**
 * Tells whether a file is a JPEG, by the type the picker reported and, when it reported none, by its extension. The API checks the
 * bytes that arrive in the storage anyway; this only keeps the phone from preparing a file that can never be a proof.
 * @param file The file the photographer chose.
 * @returns True for a JPEG.
 */
export function isJpegFile(file: Pick<PickedFile, 'name' | 'mimeType'>): boolean {
  if (file.mimeType !== null && file.mimeType !== '') {
    return file.mimeType === 'image/jpeg';
  }

  return /\.jpe?g$/i.test(file.name);
}

/**
 * Tells whether a file the photographer chose can be a ZIP: its name ends in `.zip` or the picker says it is one. Some systems report
 * `application/x-zip-compressed`, others `application/zip`, and some report nothing. The API checks what really arrives.
 * @param file The file.
 * @returns True when it can be a ZIP.
 */
export function isZipFile(file: Pick<PickedFile, 'name' | 'mimeType'>): boolean {
  return /\.zip$/i.test(file.name) || file.mimeType === 'application/zip' || file.mimeType === 'application/x-zip-compressed';
}

/**
 * Builds a key that tells a file apart from the others the photographer already chose. Two files with the same name, size and
 * dimensions are the same photo chosen twice.
 * @param file The file.
 * @returns The key.
 */
export function fileKey(file: Pick<PickedFile, 'name' | 'bytes' | 'width' | 'height'>): string {
  return `${file.name}\u0000${file.bytes}\u0000${file.width ?? ''}\u0000${file.height ?? ''}`;
}

/**
 * Sorts the files chosen as proofs: which can be uploaded and why the others cannot. The order they were chosen in is kept.
 * @param files The files chosen.
 * @param queued Files already waiting in the queue, so a photo chosen twice is not uploaded twice.
 * @param room How many more proofs the gallery can hold, counting the ones already waiting.
 * @returns The files to upload and how many were left out for each reason.
 */
export function selectProofFiles(files: readonly PickedFile[], queued: readonly PickedFile[], room: number): ProofSelection {
  const seen = new Set(queued.map(fileKey));
  const accepted: PickedFile[] = [];
  let notJpeg = 0;
  let duplicates = 0;
  let noRoom = 0;

  for (const file of files) {
    if (!isJpegFile(file)) {
      notJpeg++;
    } else if (seen.has(fileKey(file))) {
      duplicates++;
    } else if (accepted.length >= Math.max(0, room)) {
      noRoom++;
    } else {
      seen.add(fileKey(file));
      accepted.push(file);
    }
  }

  return { accepted, notJpeg, duplicates, noRoom };
}

/**
 * Says in words which files were left out of the proofs, or nothing when none was.
 * @param selection What came of the files chosen.
 * @returns The message, or null when every file was taken.
 */
export function describeSkippedProofs(selection: ProofSelection): string | null {
  const parts: string[] = [];
  if (selection.notJpeg > 0) {
    parts.push(`${selection.notJpeg} ${selection.notJpeg === 1 ? 'no es JPEG' : 'no son JPEG'}`);
  }

  if (selection.duplicates > 0) {
    parts.push(`${selection.duplicates} ${selection.duplicates === 1 ? 'ya estaba en la lista' : 'ya estaban en la lista'}`);
  }

  if (selection.noRoom > 0) {
    parts.push(`${selection.noRoom} ${selection.noRoom === 1 ? 'no cabe' : 'no caben'} en la galería`);
  }

  if (parts.length === 0) {
    return null;
  }

  const total = selection.notJpeg + selection.duplicates + selection.noRoom;
  return `${total === 1 ? 'Se dejó fuera 1 foto' : `Se dejaron fuera ${total} fotos`}: ${parts.join('; ')}.`;
}
