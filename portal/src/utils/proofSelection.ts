import type { ProofSelection } from '../types/utils/proofSelection.types';
import { isJpegFile } from './proofImage';

/**
 * Builds a key that tells a file apart from the others the person already chose. Two files with the same name, size and date are
 * the same photo chosen twice.
 * @param file The file.
 * @returns The key.
 */
export function fileKey(file: Pick<File, 'name' | 'size' | 'lastModified'>): string {
  return `${file.name}\u0000${file.size}\u0000${file.lastModified}`;
}

/**
 * Sorts the files a person chose: which can be uploaded and why the others cannot. The order they were chosen in is kept.
 * @param files The files chosen.
 * @param queued Files already waiting in the queue, so a photo chosen twice is not uploaded twice.
 * @param room How many more proofs the gallery can hold, counting the ones already waiting.
 * @returns The files to upload and how many were left out for each reason.
 */
export function selectProofFiles(files: readonly File[], queued: readonly Pick<File, 'name' | 'size' | 'lastModified'>[], room: number): ProofSelection {
  const seen = new Set(queued.map(fileKey));
  const accepted: File[] = [];
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
 * Says in words which files were left out, or nothing when none was.
 * @param selection What came of the files chosen.
 * @returns The message, or null when every file was taken.
 */
export function describeSkipped(selection: ProofSelection): string | null {
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
