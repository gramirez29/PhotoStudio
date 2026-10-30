import { GALLERIES_PAGE_SIZE, GALLERY_ACTIONS, GALLERY_STATUSES } from '../constants/gallery';
import type { HttpClient } from '../types/api/http';
import type { StudioCodeSent, StudioGallery, StudioGalleryPage, StudioLoginResult, StudioUser } from '../types/api/studio';
import type { StudioApi } from '../types/api/studioApi';
import { expectRecord, readArray, readLiteral, readNullableString, readString } from './guards';
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
    createdAt: readString(record, 'createdAt', path),
    allowedActions: readArray(record, 'allowedActions', path).flatMap(knownAction),
  };
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
  };
}
