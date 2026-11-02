import { GALLERIES_PAGE_SIZE, GALLERY_ACTIONS, GALLERY_STATUSES } from '../constants/gallery';
import type { HttpClient } from '../types/api/http';
import type { FinalDetailsInfo, FinalDownloadLink, FinalFileInfo, FinalUploadSlot, ZipUploadSlot } from '../types/api/finals';
import type { ProofFileInfo, ProofRegistration, ProofUploadSlot, RegisterProofsResult, StudioProof } from '../types/api/proofs';
import type { StudioCodeSent, StudioGallery, StudioGalleryPage, StudioLoginResult, StudioSelection, StudioUser, StudioZip } from '../types/api/studio';
import type { StudioApi } from '../types/api/studioApi';
import { expectRecord, readArray, readLiteral, readNullableString, readNumber, readString } from './guards';
import { parseMoney } from './portalApi';

/**
 * Parses the user of a web session.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed user.
 */
export function parseStudioUser(value: unknown, path: string): StudioUser {
  const record = expectRecord(value, path);
  return {
    photographerId: readString(record, 'photographerId', path),
    username: readString(record, 'username', path),
    name: readString(record, 'name', path),
    maskedEmail: readNullableString(record, 'maskedEmail', path) ?? '',
  };
}

/**
 * Parses an answer that carries the user of a session.
 * @param value Unknown response body.
 * @returns The typed user.
 */
export function parseStudioSession(value: unknown): StudioUser {
  const path = 'session';
  return parseStudioUser(expectRecord(value, path).user, `${path}.user`);
}

/**
 * Parses the answer to the password step: the session, or where the code went.
 * @param value Unknown response body.
 * @returns The typed result.
 */
export function parseStudioLogin(value: unknown): StudioLoginResult {
  const path = 'login';
  const record = expectRecord(value, path);
  const status = readLiteral(record, 'status', ['SignedIn', 'CodeRequired'] as const, path);
  if (status === 'SignedIn') {
    return { status, user: parseStudioUser(record.user, `${path}.user`) };
  }

  return { status, maskedEmail: readString(record, 'maskedEmail', path), expiresAt: readString(record, 'expiresAt', path) };
}

/**
 * Parses the confirmation that a code was sent.
 * @param value Unknown response body.
 * @returns The typed confirmation.
 */
export function parseStudioCodeSent(value: unknown): StudioCodeSent {
  const path = 'codeSent';
  const record = expectRecord(value, path);
  return { maskedEmail: readString(record, 'maskedEmail', path), expiresAt: readString(record, 'expiresAt', path) };
}

/**
 * Parses a gallery.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed gallery.
 */
export function parseStudioGallery(value: unknown, path: string): StudioGallery {
  const record = expectRecord(value, path);
  return {
    id: readString(record, 'id', path),
    bookingId: readString(record, 'bookingId', path),
    status: readLiteral(record, 'status', GALLERY_STATUSES, path),
    clientName: readString(record, 'clientName', path),
    packageName: readString(record, 'packageName', path),
    sessionStart: readString(record, 'sessionStart', path),
    includedPhotos: readCount(record, 'includedPhotos', path),
    bonusPhotos: readCount(record, 'bonusPhotos', path),
    selectablePhotos: readCount(record, 'selectablePhotos', path),
    extraPhotoPrice: parseMoney(record.extraPhotoPrice, `${path}.extraPhotoPrice`),
    maxPhotos: readCount(record, 'maxPhotos', path),
    photoCount: readCount(record, 'photoCount', path),
    selectedPhotoCount: readCount(record, 'selectedPhotoCount', path),
    extraPhotoCount: readCount(record, 'extraPhotoCount', path),
    selectionSubmittedAt: readNullableString(record, 'selectionSubmittedAt', path),
    createdAt: readString(record, 'createdAt', path),
    allowedActions: readArray(record, 'allowedActions', path).flatMap(knownAction),
    finalsPublishedAt: readNullableString(record, 'finalsPublishedAt', path),
    deliveredAt: readNullableString(record, 'deliveredAt', path),
    extrasReleasedAt: readNullableString(record, 'extrasReleasedAt', path),
    zip: record.zip === null || record.zip === undefined ? null : parseStudioZip(record.zip, `${path}.zip`),
    selectionReopenedAt: readNullableString(record, 'selectionReopenedAt', path),
    selectionReopenReason: readNullableString(record, 'selectionReopenReason', path),
  };
}

/**
 * Parses the ZIP of a gallery.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed ZIP.
 */
export function parseStudioZip(value: unknown, path: string): StudioZip {
  const record = expectRecord(value, path);
  return {
    bytes: readNumber(record, 'bytes', path),
    uploadedAt: readString(record, 'uploadedAt', path),
    downloads: readCount(record, 'downloads', path),
    lastDownloadedAt: readNullableString(record, 'lastDownloadedAt', path),
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
  const headers = expectRecord(record.headers, `${path}.headers`);
  return {
    url: readString(record, 'url', path),
    headers: Object.fromEntries(Object.keys(headers).map((name) => [name, readString(headers, name, `${path}.headers`)])),
    expiresAt: readString(record, 'expiresAt', path),
  };
}

/**
 * Parses the address to download a final.
 * @param value Unknown response body.
 * @returns The address and the name to save the file with.
 */
export function parseFinalDownload(value: unknown): FinalDownloadLink {
  const path = 'finalDownload';
  const record = expectRecord(value, path);
  return { fileName: readString(record, 'fileName', path), url: readString(record, 'url', path) };
}

/**
 * Parses one page of galleries.
 * @param value Unknown response body.
 * @returns The typed page.
 */
export function parseStudioGalleryPage(value: unknown): StudioGalleryPage {
  const path = 'galleries';
  const record = expectRecord(value, path);
  return {
    items: readArray(record, 'items', path).map((item, index) => parseStudioGallery(item, `${path}.items[${index}]`)),
    nextCursor: readNullableString(record, 'nextCursor', path),
  };
}

/**
 * Parses the places to upload the proofs of a request.
 * @param value Unknown response body.
 * @returns One slot per file, in the order of the request.
 */
export function parseProofSlots(value: unknown): readonly ProofUploadSlot[] {
  const path = 'proofUploads';
  return readArray(expectRecord(value, path), 'slots', path).map((item, index): ProofUploadSlot => {
    const slotPath = `${path}.slots[${index}]`;
    const record = expectRecord(item, slotPath);
    const headers = expectRecord(record.headers, `${slotPath}.headers`);
    return {
      photoId: readString(record, 'photoId', slotPath),
      url: readString(record, 'url', slotPath),
      headers: Object.fromEntries(Object.keys(headers).map((name) => [name, readString(headers, name, `${slotPath}.headers`)])),
      expiresAt: readString(record, 'expiresAt', slotPath),
    };
  });
}

/**
 * Parses the places to upload the finals of a request, each with the place for its thumbnail.
 * @param value Unknown response body.
 * @returns One slot per file, in the order of the request.
 */
export function parseFinalSlots(value: unknown): readonly FinalUploadSlot[] {
  const path = 'finalUploads';
  return readArray(expectRecord(value, path), 'slots', path).map((item, index): FinalUploadSlot => {
    const slotPath = `${path}.slots[${index}]`;
    const record = expectRecord(item, slotPath);
    const thumbHeaders = record.thumbHeaders === null || record.thumbHeaders === undefined ? {} : expectRecord(record.thumbHeaders, `${slotPath}.thumbHeaders`);
    return {
      ...parseProofSlots({ slots: [item] })[0]!,
      thumbUrl: readNullableString(record, 'thumbUrl', slotPath),
      thumbHeaders: Object.fromEntries(Object.keys(thumbHeaders).map((name) => [name, readString(thumbHeaders, name, `${slotPath}.thumbHeaders`)])),
    };
  });
}

/**
 * Parses the outcome of registering a batch of proofs.
 * @param value Unknown response body.
 * @returns The gallery and the proofs that were not registered.
 */
export function parseRegisterProofs(value: unknown): RegisterProofsResult {
  const path = 'registerProofs';
  const record = expectRecord(value, path);
  return {
    gallery: parseStudioGallery(record.gallery, `${path}.gallery`),
    rejected: readArray(record, 'rejected', path).map((item, index) => {
      const itemPath = `${path}.rejected[${index}]`;
      const rejection = expectRecord(item, itemPath);
      return { photoId: readString(rejection, 'photoId', itemPath), reason: readString(rejection, 'reason', itemPath) };
    }),
  };
}

/**
 * Parses the proofs of a gallery.
 * @param value Unknown response body.
 * @returns The proofs, in the order they were registered.
 */
export function parseProofList(value: unknown): readonly StudioProof[] {
  const path = 'proofs';
  return readArray(expectRecord(value, path), 'items', path).map((item, index): StudioProof => {
    const itemPath = `${path}.items[${index}]`;
    const record = expectRecord(item, itemPath);
    return {
      id: readString(record, 'id', itemPath),
      name: readString(record, 'name', itemPath),
      bytes: readNumber(record, 'bytes', itemPath),
      url: readString(record, 'url', itemPath),
      expiresAt: readString(record, 'expiresAt', itemPath),
    };
  });
}

/**
 * Parses what the client chose.
 * @param value Unknown response body.
 * @returns The typed selection.
 */
export function parseStudioSelection(value: unknown): StudioSelection {
  const path = 'selection';
  const record = expectRecord(value, path);
  return {
    submittedAt: readNullableString(record, 'submittedAt', path),
    extraPhotos: readCount(record, 'extraPhotos', path),
    extraTotal: parseMoney(record.extraTotal, `${path}.extraTotal`),
    photos: readArray(record, 'photos', path).map((item, index) => {
      const itemPath = `${path}.photos[${index}]`;
      const photo = expectRecord(item, itemPath);
      return {
        id: readString(photo, 'id', itemPath),
        name: readString(photo, 'name', itemPath),
        isExtra: photo.isExtra === true,
        finalBytes: photo.finalBytes === null || photo.finalBytes === undefined ? null : readNumber(photo, 'finalBytes', itemPath),
        finalWidth: photo.finalWidth === null || photo.finalWidth === undefined ? null : readNumber(photo, 'finalWidth', itemPath),
        finalHeight: photo.finalHeight === null || photo.finalHeight === undefined ? null : readNumber(photo, 'finalHeight', itemPath),
        finalCapturedAt: readNullableString(photo, 'finalCapturedAt', itemPath),
        finalDownloads: photo.finalDownloads === null || photo.finalDownloads === undefined ? 0 : readNumber(photo, 'finalDownloads', itemPath),
        thumbUrl: readNullableString(photo, 'thumbUrl', itemPath),
        thumbIsFinal: photo.thumbIsFinal === true,
      };
    }),
  };
}

/**
 * Parses a single gallery answer.
 * @param value Unknown response body.
 * @returns The typed gallery.
 */
export function parseStudioGalleryBody(value: unknown): StudioGallery {
  return parseStudioGallery(value, 'gallery');
}

/**
 * Reads a count of photos: a whole number, never negative.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The count.
 */
function readCount(record: Record<string, unknown>, key: string, path: string): number {
  const value = record[key];
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 0) {
    throw new Error(`${path}.${key} should be a whole number of photos`);
  }

  return value;
}

/**
 * Keeps an action the page knows. An action it does not know is left out, not an error: the API may learn to do more than this
 * version of the page does, and the page must keep working until it is updated.
 * @param value Unknown value from the response.
 * @returns The action in a list of one, or an empty list.
 */
function knownAction(value: unknown): readonly (typeof GALLERY_ACTIONS)[number][] {
  const action = GALLERY_ACTIONS.find((candidate) => candidate === value);
  return action === undefined ? [] : [action];
}

/**
 * Creates the web area API on top of an HTTP client that sends the cookies.
 * @param client HTTP client with credentials.
 * @returns The web area API.
 */
export function createStudioApi(client: HttpClient): StudioApi {
  return {
    getSession: (signal) => client.get('/api/web/auth/session', parseStudioSession, { signal }),
    login: (username, password) => client.post('/api/web/auth/login', { username, password }, parseStudioLogin),
    verifyCode: (code, rememberDevice) => client.post('/api/web/auth/code/verify', { code, rememberDevice }, parseStudioSession),
    resendCode: () => client.post('/api/web/auth/code/resend', {}, parseStudioCodeSent),
    logout: (forgetDevice) => client.post('/api/web/auth/logout', { forgetDevice }, () => undefined),
    listGalleries: (cursor, signal) =>
      client.get(
        `/api/web/galleries?limit=${GALLERIES_PAGE_SIZE}${cursor === null ? '' : `&cursor=${encodeURIComponent(cursor)}`}`,
        parseStudioGalleryPage,
        { signal },
      ),
    getGallery: (galleryId, signal) => client.get(`/api/web/galleries/${galleryId}`, parseStudioGalleryBody, { signal }),
    listArchivable: (signal) => client.get('/api/web/galleries/archivable', (body) => parseStudioGalleryPage(body).items, { signal }),
    getSelection: (galleryId, signal) => client.get(`/api/web/galleries/${galleryId}/selection`, parseStudioSelection, { signal }),
    listProofs: (galleryId, signal) => client.get(`/api/web/galleries/${galleryId}/proofs`, parseProofList, { signal }),
    requestProofUploads: (galleryId, files: readonly ProofFileInfo[], signal) =>
      client.post(`/api/web/galleries/${galleryId}/proofs/uploads`, { files }, parseProofSlots, { signal }),
    registerProofs: (galleryId, photos: readonly ProofRegistration[], signal) =>
      client.post(`/api/web/galleries/${galleryId}/proofs`, { photos }, parseRegisterProofs, { signal }),
    removeProof: (galleryId, photoId) => client.post(`/api/web/galleries/${galleryId}/proofs/${photoId}/remove`, {}, parseStudioGalleryBody),
    reopenSelection: (galleryId, reason) => client.post(`/api/web/galleries/${galleryId}/selection/reopen`, { reason }, parseStudioGalleryBody),
    openSelection: (galleryId) => client.post(`/api/web/galleries/${galleryId}/selection/open`, {}, parseStudioGalleryBody),
    requestFinalUploads: (galleryId, files: readonly FinalFileInfo[], signal) =>
      client.post(`/api/web/galleries/${galleryId}/finals/uploads`, { files }, parseFinalSlots, { signal }),
    registerFinals: (galleryId, photoIds: readonly string[], details: readonly FinalDetailsInfo[], signal) =>
      client.post(`/api/web/galleries/${galleryId}/finals`, { photoIds, details }, parseRegisterProofs, { signal }),
    requestZipUpload: (galleryId, bytes, signal) => client.post(`/api/web/galleries/${galleryId}/zip/upload`, { bytes }, parseZipSlot, { signal }),
    registerZip: (galleryId, signal) => client.post(`/api/web/galleries/${galleryId}/zip`, {}, parseStudioGalleryBody, { signal }),
    removeZip: (galleryId) => client.post(`/api/web/galleries/${galleryId}/zip/remove`, {}, parseStudioGalleryBody),
    releaseGallery: (galleryId) => client.post(`/api/web/galleries/${galleryId}/release`, {}, parseStudioGalleryBody),
    getFinalDownload: (galleryId, photoId, signal) => client.get(`/api/web/galleries/${galleryId}/finals/${photoId}/download`, parseFinalDownload, { signal }),
    publishFinals: (galleryId) => client.post(`/api/web/galleries/${galleryId}/finals/publish`, {}, parseStudioGalleryBody),
    releaseExtras: (galleryId) => client.post(`/api/web/galleries/${galleryId}/extras/release`, {}, parseStudioGalleryBody),
  };
}
