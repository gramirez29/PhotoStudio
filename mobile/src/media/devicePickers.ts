import * as DocumentPicker from 'expo-document-picker';
import { File } from 'expo-file-system';
import * as ImagePicker from 'expo-image-picker';
import { ZIP_MIME_TYPES } from '../constants/upload';
import type { PickedFile } from '../types/utils/upload.types';
import { readCapturedAt } from '../utils/exifDate';

/**
 * Reads the size of a file on the phone, for when the picker did not say.
 * @param uri Address of the file.
 * @returns The size in bytes, or 0 when it cannot be read.
 */
function sizeOf(uri: string): number {
  try {
    return new File(uri).size;
  } catch {
    return 0;
  }
}

/**
 * Takes the name of a file out of its address, for when the picker gave none.
 * @param uri Address of the file.
 * @returns The last part of the address.
 */
function nameFromUri(uri: string): string {
  return decodeURIComponent(uri.slice(uri.lastIndexOf('/') + 1)) || 'foto.jpg';
}

/**
 * Lets the photographer choose photos from the library of the phone. The system's own picker is used, so the app asks for no
 * permission and sees only what the photographer chooses. The photos come as they are (no cropping and no recompression) with
 * their metadata, to read the date they were taken.
 * @param limit Most photos the photographer can choose now.
 * @returns The photos chosen, in the order the picker gave them; empty when the photographer cancelled.
 */
export async function pickPhotos(limit: number): Promise<readonly PickedFile[]> {
  const result = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ['images'],
    allowsMultipleSelection: true,
    selectionLimit: Math.max(1, limit),
    allowsEditing: false,
    quality: 1,
    exif: true,
  });
  if (result.canceled) {
    return [];
  }

  const now = Date.now();
  return result.assets.map(
    (asset): PickedFile => ({
      uri: asset.uri,
      name: asset.fileName ?? nameFromUri(asset.uri),
      bytes: asset.fileSize ?? sizeOf(asset.uri),
      mimeType: asset.mimeType ?? null,
      width: asset.width > 0 ? asset.width : null,
      height: asset.height > 0 ? asset.height : null,
      capturedAt: readCapturedAt(asset.exif, now),
    }),
  );
}

/**
 * Lets the photographer choose a ZIP from the files of the phone. A copy goes to the cache of the app, which is deleted when the ZIP
 * is uploaded.
 * @returns The file chosen, or null when the photographer cancelled.
 */
export async function pickZip(): Promise<PickedFile | null> {
  const result = await DocumentPicker.getDocumentAsync({ type: [...ZIP_MIME_TYPES], multiple: false, copyToCacheDirectory: true });
  const asset = result.canceled ? undefined : result.assets[0];
  if (asset === undefined) {
    return null;
  }

  return {
    uri: asset.uri,
    name: asset.name,
    bytes: asset.size ?? sizeOf(asset.uri),
    mimeType: asset.mimeType ?? null,
    width: null,
    height: null,
    capturedAt: null,
  };
}
