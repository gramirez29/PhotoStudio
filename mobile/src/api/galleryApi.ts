import { GALLERY_ACTIONS, GALLERY_STATUSES } from '../constants/gallery';
import type {
  FinalDownloadResponse,
  FinalUploadSlot,
  GalleryAction,
  GalleryResponse,
  GalleryZip,
  ProofResponse,
  ProofUploadSlot,
  RegisterUploadsResult,
  SelectedPhoto,
  SelectionResponse,
  ZipUploadSlot,
} from '../types/api/gallery';
import type { GalleryApi } from '../types/api/galleryApi';
import type { HttpClient } from '../types/api/http';
import { parseMoney } from './bookingsApi';
import { expectRecord, readArray, readLiteral, readNullableString, readNumber, readString } from './guards';

/**
 * Reads a number that may be missing or null, for a field an older API did not send.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The number, or null.
 */
function readNullableNumber(record: Record<string, unknown>, key: string, path: string): number | null {
  const value = record[key];
  return value === null || value === undefined ? null : readNumber(record, key, path);
}

/**
 * Keeps only the actions this version of the app knows, so an action added by the API later does not break the screen.
 * @param value One element of the `allowedActions` array.
 * @returns The action in a list of one, or an empty list.
 */
function knownAction(value: unknown): readonly GalleryAction[] {
  const action = GALLERY_ACTIONS.find((candidate) => candidate === value);
  return action === undefined ? [] : [action];
}

/**
 * Parses the ZIP of a gallery.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed ZIP.
 */
export function parseGalleryZip(value: unknown, path: string): GalleryZip {
  const record = expectRecord(value, path);
  return {
    bytes: readNumber(record, 'bytes', path),
    uploadedAt: readString(record, 'uploadedAt', path),
    downloads: readNumber(record, 'downloads', path),
    lastDownloadedAt: readNullableString(record, 'lastDownloadedAt', path),
  };
}

/**
 * Parses a gallery.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed gallery.
 */
export function parseGallery(value: unknown, path = 'gallery'): GalleryResponse {
  const record = expectRecord(value, path);
  return {
    id: readString(record, 'id', path),
    bookingId: readString(record, 'bookingId', path),
    status: readLiteral(record, 'status', GALLERY_STATUSES, path),
    clientName: readString(record, 'clientName', path),
    packageName: readString(record, 'packageName', path),
    sessionStart: readString(record, 'sessionStart', path),
    includedPhotos: readNumber(record, 'includedPhotos', path),
    bonusPhotos: readNumber(record, 'bonusPhotos', path),
    selectablePhotos: readNumber(record, 'selectablePhotos', path),
    extraPhotoPrice: parseMoney(record.extraPhotoPrice, `${path}.extraPhotoPrice`),
    maxPhotos: readNumber(record, 'maxPhotos', path),
    photoCount: readNumber(record, 'photoCount', path),
    createdAt: readString(record, 'createdAt', path),
    allowedActions: readArray(record, 'allowedActions', path).flatMap(knownAction),
    selectedPhotoCount: readNumber(record, 'selectedPhotoCount', path),
    extraPhotoCount: readNumber(record, 'extraPhotoCount', path),
    selectionSubmittedAt: readNullableString(record, 'selectionSubmittedAt', path),
    finalsPublishedAt: readNullableString(record, 'finalsPublishedAt', path),
    deliveredAt: readNullableString(record, 'deliveredAt', path),
    extrasReleasedAt: readNullableString(record, 'extrasReleasedAt', path),
    zip: record.zip === null || record.zip === undefined ? null : parseGalleryZip(record.zip, `${path}.zip`),
    selectionReopenedAt: readNullableString(record, 'selectionReopenedAt', path),
    selectionReopenReason: readNullableString(record, 'selectionReopenReason', path),
  };
}

/**
 * Parses what the client chose.
 * @param value Unknown response body.
 * @returns The typed selection.
 */
export function parseSelection(value: unknown): SelectionResponse {
  const path = 'selection';
  const record = expectRecord(value, path);
  return {
    submittedAt: readNullableString(record, 'submittedAt', path),
    extraPhotos: readNumber(record, 'extraPhotos', path),
    extraTotal: parseMoney(record.extraTotal, `${path}.extraTotal`),
    photos: readArray(record, 'photos', path).map((item, index): SelectedPhoto => {
      const itemPath = `${path}.photos[${index}]`;
      const photo = expectRecord(item, itemPath);
      return {
        id: readString(photo, 'id', itemPath),
        name: readString(photo, 'name', itemPath),
        isExtra: photo.isExtra === true,
        finalBytes: readNullableNumber(photo, 'finalBytes', itemPath),
        finalWidth: readNullableNumber(photo, 'finalWidth', itemPath),
        finalHeight: readNullableNumber(photo, 'finalHeight', itemPath),
        finalCapturedAt: readNullableString(photo, 'finalCapturedAt', itemPath),
        finalDownloads: readNullableNumber(photo, 'finalDownloads', itemPath) ?? 0,
        thumbUrl: readNullableString(photo, 'thumbUrl', itemPath),
        thumbIsFinal: photo.thumbIsFinal === true,
      };
    }),
  };
}

/**
 * Parses the proofs of a gallery.
 * @param value Unknown response body.
 * @returns The proofs, in the order they were uploaded.
 */
export function parseProofs(value: unknown): readonly ProofResponse[] {
  const path = 'proofs';
  return readArray(expectRecord(value, path), 'items', path).map((item, index): ProofResponse => {
    const itemPath = `${path}.items[${index}]`;
    const proof = expectRecord(item, itemPath);
    return {
      id: readString(proof, 'id', itemPath),
      name: readString(proof, 'name', itemPath),
      bytes: readNumber(proof, 'bytes', itemPath),
      url: readString(proof, 'url', itemPath),
      expiresAt: readString(proof, 'expiresAt', itemPath),
    };
  });
}

/**
 * Parses the address to download a final.
 * @param value Unknown response body.
 * @returns The address.
 */
export function parseFinalDownload(value: unknown): FinalDownloadResponse {
  const path = 'finalDownload';
  const record = expectRecord(value, path);
  return {
    fileName: readString(record, 'fileName', path),
    url: readString(record, 'url', path),
    expiresAt: readString(record, 'expiresAt', path),
  };
}

/**
 * Reads the headers a signed address must be used with.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The headers, by name.
 */
function parseHeaders(value: unknown, path: string): Readonly<Record<string, string>> {
  const headers = expectRecord(value, path);
  return Object.fromEntries(Object.keys(headers).map((name) => [name, readString(headers, name, path)]));
}

/**
 * Parses the places to upload some proofs.
 * @param value Unknown response body.
 * @returns One slot per file, in the order of the request.
 */
export function parseProofSlots(value: unknown): readonly ProofUploadSlot[] {
  const path = 'proofUploads';
  return readArray(expectRecord(value, path), 'slots', path).map((item, index): ProofUploadSlot => {
    const slotPath = `${path}.slots[${index}]`;
    const slot = expectRecord(item, slotPath);
    return {
      photoId: readString(slot, 'photoId', slotPath),
      url: readString(slot, 'url', slotPath),
      headers: parseHeaders(slot.headers, `${slotPath}.headers`),
      expiresAt: readString(slot, 'expiresAt', slotPath),
    };
  });
}

/**
 * Parses the places to upload some finals, each with the place for its thumbnail.
 * @param value Unknown response body.
 * @returns One slot per file, in the order of the request.
 */
export function parseFinalSlots(value: unknown): readonly FinalUploadSlot[] {
  const path = 'finalUploads';
  const slots = readArray(expectRecord(value, path), 'slots', path);
  return parseProofSlots(value).map((slot, index): FinalUploadSlot => {
    const slotPath = `${path}.slots[${index}]`;
    const record = expectRecord(slots[index], slotPath);
    return {
      ...slot,
      thumbUrl: readNullableString(record, 'thumbUrl', slotPath),
      thumbHeaders: record.thumbHeaders === null || record.thumbHeaders === undefined ? {} : parseHeaders(record.thumbHeaders, `${slotPath}.thumbHeaders`),
    };
  });
}

/**
 * Parses what registering a batch of proofs or finals gave back.
 * @param value Unknown response body.
 * @returns The gallery and the uploads that were not registered.
 */
export function parseRegisterUploads(value: unknown): RegisterUploadsResult {
  const path = 'registerUploads';
  const record = expectRecord(value, path);
  return {
    gallery: parseGallery(record.gallery, `${path}.gallery`),
    rejected: readArray(record, 'rejected', path).map((item, index) => {
      const itemPath = `${path}.rejected[${index}]`;
      const rejection = expectRecord(item, itemPath);
      return { photoId: readString(rejection, 'photoId', itemPath), reason: readString(rejection, 'reason', itemPath) };
    }),
  };
}

/**
 * Parses the place to upload the ZIP.
 * @param value Unknown response body.
 * @returns The place.
 */
export function parseZipSlot(value: unknown): ZipUploadSlot {
  const path = 'zipUpload';
  const record = expectRecord(value, path);
  return {
    url: readString(record, 'url', path),
    headers: parseHeaders(record.headers, `${path}.headers`),
    expiresAt: readString(record, 'expiresAt', path),
  };
}

/**
 * Creates the gallery API on top of an HTTP client.
 * @param client HTTP client that sends the token of the session.
 * @returns The gallery API.
 */
export function createGalleryApi(client: HttpClient): GalleryApi {
  const path = (galleryId: string, suffix = ''): string => `/api/galleries/${encodeURIComponent(galleryId)}${suffix}`;
  const post = (galleryId: string, suffix: string, body: unknown = {}): Promise<GalleryResponse> =>
    client.post(path(galleryId, suffix), body, (response) => parseGallery(response));

  return {
    getByBooking: (bookingId, signal) =>
      client.get(`/api/galleries/by-booking/${encodeURIComponent(bookingId)}`, (body) => parseGallery(body), signal),
    get: (galleryId, signal) => client.get(path(galleryId), (body) => parseGallery(body), signal),
    getSelection: (galleryId, signal) => client.get(path(galleryId, '/selection'), parseSelection, signal),
    listProofs: (galleryId, signal) => client.get(path(galleryId, '/proofs'), parseProofs, signal),
    editTerms: (galleryId, terms) => post(galleryId, '/terms', terms),
    removeProof: (galleryId, photoId) => post(galleryId, `/proofs/${encodeURIComponent(photoId)}/remove`),
    openSelection: (galleryId) => post(galleryId, '/selection/open'),
    reopenSelection: (galleryId, reason) => post(galleryId, '/selection/reopen', { reason }),
    publishFinals: (galleryId) => post(galleryId, '/finals/publish'),
    releaseExtras: (galleryId) => post(galleryId, '/extras/release'),
    removeZip: (galleryId) => post(galleryId, '/zip/remove'),
    getFinalDownload: (galleryId, photoId, signal) =>
      client.get(path(galleryId, `/finals/${encodeURIComponent(photoId)}/download`), parseFinalDownload, signal),
    requestProofUploads: (galleryId, files, signal) => client.post(path(galleryId, '/proofs/uploads'), { files }, parseProofSlots, signal),
    registerProofs: (galleryId, photos, signal) => client.post(path(galleryId, '/proofs'), { photos }, parseRegisterUploads, signal),
    requestFinalUploads: (galleryId, files, signal) => client.post(path(galleryId, '/finals/uploads'), { files }, parseFinalSlots, signal),
    registerFinals: (galleryId, photoIds, details, signal) => client.post(path(galleryId, '/finals'), { photoIds, details }, parseRegisterUploads, signal),
    requestZipUpload: (galleryId, bytes, signal) => client.post(path(galleryId, '/zip/upload'), { bytes }, parseZipSlot, signal),
    registerZip: (galleryId, signal) => client.post(path(galleryId, '/zip'), {}, (body) => parseGallery(body), signal),
  };
}
