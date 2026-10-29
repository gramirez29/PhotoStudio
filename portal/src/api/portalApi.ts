import { BOOKING_STATUSES, PAYMENT_STATUSES } from '../constants/booking';
import type { HttpClient } from '../types/api/http';
import type { Money, PortalBooking, PortalCodeSent, PortalContract, PortalPayment, PortalSession } from '../types/api/portal';
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
  };
}
