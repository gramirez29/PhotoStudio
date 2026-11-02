import { BOOKING_STATUSES, PAYMENT_STATUSES } from '../constants/booking';
import type { HttpClient } from '../types/api/http';
import { GALLERY_STATUSES } from '../constants/gallery';
import type {
  Money,
  PortalBooking,
  PortalCodeSent,
  PortalContract,
  PortalExtra,
  PortalDownload,
  PortalFinal,
  PortalGallery,
  PortalGallerySummary,
  PortalPayment,
  PortalProof,
  PortalSelection,
  PortalSession,
  PortalZipDownload,
} from '../types/api/portal';
import type { PortalApi } from '../types/api/portalApi';
import { expectRecord, readArray, readBoolean, readLiteral, readNullableString, readNumber, readString } from './guards';

/**
 * Parses a monetary amount.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed amount.
 */
export function parseMoney(value: unknown, path: string): Money {
  const record = expectRecord(value, path);
  return { amount: readNumber(record, 'amount', path), currency: readString(record, 'currency', path) };
}

/**
 * Parses a payment.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed payment.
 */
export function parsePayment(value: unknown, path: string): PortalPayment {
  const record = expectRecord(value, path);
  return {
    amount: parseMoney(record.amount, `${path}.amount`),
    status: readLiteral(record, 'status', PAYMENT_STATUSES, path),
    recordedAt: readString(record, 'recordedAt', path),
  };
}

/**
 * Parses an extra sold with the booking.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed extra.
 */
export function parseExtra(value: unknown, path: string): PortalExtra {
  const record = expectRecord(value, path);
  return {
    name: readString(record, 'name', path),
    quantity: readNumber(record, 'quantity', path),
    total: parseMoney(record.total, `${path}.total`),
  };
}

/**
 * Parses the booking a link opens, validating every field against the backend contract.
 * @param value Unknown response body.
 * @returns The typed booking.
 */
export function parsePortalBooking(value: unknown): PortalBooking {
  const path = 'booking';
  const record = expectRecord(value, path);
  return {
    photographerName: readString(record, 'photographerName', path),
    clientName: readString(record, 'clientName', path),
    packageName: readString(record, 'packageName', path),
    status: readLiteral(record, 'status', BOOKING_STATUSES, path),
    sessionStart: readString(record, 'sessionStart', path),
    sessionEnd: readString(record, 'sessionEnd', path),
    expiresAt: readNullableString(record, 'expiresAt', path),
    packagePrice: parseMoney(record.packagePrice, `${path}.packagePrice`),
    depositRequired: parseMoney(record.depositRequired, `${path}.depositRequired`),
    totalPaid: parseMoney(record.totalPaid, `${path}.totalPaid`),
    balance: parseMoney(record.balance, `${path}.balance`),
    contractSigned: readBoolean(record, 'contractSigned', path),
    payments: readArray(record, 'payments', path).map((payment, index) => parsePayment(payment, `${path}.payments[${index}]`)),
    canSubmitProof: readBoolean(record, 'canSubmitProof', path),
    canReceiveCode: readBoolean(record, 'canReceiveCode', path),
    canSignContract: readBoolean(record, 'canSignContract', path),
    basePrice: parseMoney(record.basePrice, `${path}.basePrice`),
    extras: readArray(record, 'extras', path).map((extra, index) => parseExtra(extra, `${path}.extras[${index}]`)),
    discount: parseMoney(record.discount, `${path}.discount`),
  };
}

/**
 * Parses the confirmation that a verification code was sent.
 * @param value Unknown response body.
 * @returns The typed confirmation.
 */
export function parseCodeSent(value: unknown): PortalCodeSent {
  const path = 'codeSent';
  const record = expectRecord(value, path);
  return { maskedEmail: readString(record, 'maskedEmail', path), expiresAt: readString(record, 'expiresAt', path) };
}

/**
 * Parses the portal session issued after a code was verified.
 * @param value Unknown response body.
 * @returns The typed session.
 */
export function parsePortalSession(value: unknown): PortalSession {
  const path = 'session';
  const record = expectRecord(value, path);
  return { accessToken: readString(record, 'accessToken', path), expiresAt: readString(record, 'expiresAt', path) };
}

/**
 * Parses the contract of a booking.
 * @param value Unknown response body.
 * @returns The typed contract.
 */
export function parsePortalContract(value: unknown): PortalContract {
  const path = 'contract';
  const record = expectRecord(value, path);
  return {
    templateVersion: readString(record, 'templateVersion', path),
    text: readString(record, 'text', path),
    signed: readBoolean(record, 'signed', path),
  };
}

/**
 * Parses what the link says about the gallery.
 * @param value Unknown response body.
 * @returns The typed summary.
 */
export function parseGallerySummary(value: unknown): PortalGallerySummary {
  const path = 'gallerySummary';
  return { status: readLiteral(expectRecord(value, path), 'status', GALLERY_STATUSES, path) };
}

/**
 * Parses a selection that was sent.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed selection.
 */
function parseSelection(value: unknown, path: string): PortalSelection {
  const record = expectRecord(value, path);
  return {
    photoIds: readArray(record, 'photoIds', path).map((id, index) => {
      if (typeof id !== 'string') {
        throw new Error(`${path}.photoIds[${index}] should be a string`);
      }

      return id;
    }),
    submittedAt: readString(record, 'submittedAt', path),
    extraPhotos: readNumber(record, 'extraPhotos', path),
    extraTotal: parseMoney(record.extraTotal, `${path}.extraTotal`),
  };
}

/**
 * Parses the gallery of a booking.
 * @param value Unknown response body.
 * @returns The typed gallery.
 */
export function parsePortalGallery(value: unknown): PortalGallery {
  const path = 'gallery';
  const record = expectRecord(value, path);
  return {
    status: readLiteral(record, 'status', GALLERY_STATUSES, path),
    selectablePhotos: readNumber(record, 'selectablePhotos', path),
    extraPhotoPrice: parseMoney(record.extraPhotoPrice, `${path}.extraPhotoPrice`),
    photos: readArray(record, 'photos', path).map((item, index): PortalProof => {
      const itemPath = `${path}.photos[${index}]`;
      const photo = expectRecord(item, itemPath);
      return { id: readString(photo, 'id', itemPath), url: readString(photo, 'url', itemPath), expiresAt: readString(photo, 'expiresAt', itemPath) };
    }),
    selection: record.selection === null || record.selection === undefined ? null : parseSelection(record.selection, `${path}.selection`),
    finals:
      record.finals === null || record.finals === undefined
        ? []
        : readArray(record, 'finals', path).map((item, index): PortalFinal => {
            const itemPath = `${path}.finals[${index}]`;
            const final = expectRecord(item, itemPath);
            return {
              id: readString(final, 'id', itemPath),
              fileName: readString(final, 'fileName', itemPath),
              bytes: readNumber(final, 'bytes', itemPath),
              isExtra: readBoolean(final, 'isExtra', itemPath),
              thumbUrl: readNullableString(final, 'thumbUrl', itemPath),
            };
          }),
    extrasReleased: record.extrasReleased === true,
    zip: record.zip === null || record.zip === undefined ? null : { bytes: readNumber(expectRecord(record.zip, `${path}.zip`), 'bytes', `${path}.zip`) },
  };
}

/**
 * Parses the addresses to download the photos the client asked for.
 * @param value Unknown response body.
 * @returns One address per photo.
 */
export function parseDownloads(value: unknown): readonly PortalDownload[] {
  const path = 'downloads';
  return readArray(expectRecord(value, path), 'items', path).map((item, index): PortalDownload => {
    const itemPath = `${path}.items[${index}]`;
    const download = expectRecord(item, itemPath);
    return {
      id: readString(download, 'id', itemPath),
      fileName: readString(download, 'fileName', itemPath),
      url: readString(download, 'url', itemPath),
      expiresAt: readString(download, 'expiresAt', itemPath),
    };
  });
}

/**
 * Parses the address to look at a final in its real quality.
 * @param value Unknown response body.
 * @returns The address.
 */
export function parseFinalView(value: unknown): { readonly url: string } {
  const path = 'finalView';
  return { url: readString(expectRecord(value, path), 'url', path) };
}

/**
 * Parses the address to download the ZIP.
 * @param value Unknown response body.
 * @returns The address and what the client needs to know about the file.
 */
export function parseZipDownload(value: unknown): PortalZipDownload {
  const path = 'zipDownload';
  const record = expectRecord(value, path);
  return {
    fileName: readString(record, 'fileName', path),
    url: readString(record, 'url', path),
    expiresAt: readString(record, 'expiresAt', path),
    bytes: readNumber(record, 'bytes', path),
  };
}

/**
 * Creates the portal API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The portal API.
 */
export function createPortalApi(client: HttpClient): PortalApi {
  /**
   * Builds the path of an endpoint of a booking link; the token is encoded so it can never change the path.
   * @param token Secret token taken from the URL.
   * @param suffix Rest of the path, starting with a slash, or empty.
   * @returns The path.
   */
  const pathOf = (token: string, suffix = ''): string => `/public/bookings/${encodeURIComponent(token)}${suffix}`;

  return {
    getBooking: (token, signal) => client.get(pathOf(token), parsePortalBooking, { signal }),
    requestCode: (token, signal) => client.post(pathOf(token, '/code'), undefined, parseCodeSent, { signal }),
    verifyCode: (token, code, signal) => client.post(pathOf(token, '/code/verify'), { code }, parsePortalSession, { signal }),
    submitProof: (token, accessToken, request, signal) =>
      client.post(pathOf(token, '/payments'), request, parsePortalBooking, { accessToken, signal }),
    getContract: (token, accessToken, signal) => client.get(pathOf(token, '/contract'), parsePortalContract, { accessToken, signal }),
    signContract: (token, accessToken, request, signal) =>
      client.post(pathOf(token, '/contract'), request, parsePortalBooking, { accessToken, signal }),
    getGallerySummary: (token, signal) => client.get(pathOf(token, '/gallery/summary'), parseGallerySummary, { signal }),
    getGallery: (token, accessToken, signal) => client.get(pathOf(token, '/gallery'), parsePortalGallery, { accessToken, signal }),
    requestDownloads: (token, accessToken, photoIds, signal) =>
      client.post(pathOf(token, '/gallery/downloads'), { photoIds }, parseDownloads, { accessToken, signal }),
    getFinalView: async (token, accessToken, photoId, signal) =>
      (await client.get(pathOf(token, `/gallery/finals/${encodeURIComponent(photoId)}/view`), parseFinalView, { accessToken, signal })).url,
    requestZipDownload: (token, accessToken, signal) => client.post(pathOf(token, '/gallery/zip/download'), {}, parseZipDownload, { accessToken, signal }),
    submitSelection: (token, accessToken, photoIds, acceptExtras, signal) =>
      client.post(pathOf(token, '/gallery/selection'), { photoIds, acceptExtras }, parsePortalGallery, { accessToken, signal }),
  };
}
