import {
  expectArray,
  expectRecord,
  readArray,
  readLiteral,
  readNullableString,
  readNumber,
  readString,
} from './guards';
import type { HttpClient } from './httpClient';
import {
  BOOKING_ACTIONS,
  BOOKING_STATUSES,
  CHANNELS,
  PAYMENT_METHODS,
  PAYMENT_STATUSES,
  type BookingAction,
  type BookingResponse,
  type BookingSummaryResponse,
  type ContractResponse,
  type CreateBookingRequest,
  type MoneyResponse,
  type PaymentResponse,
} from './types';

/**
 * Parses a monetary amount.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed amount.
 */
export function parseMoney(value: unknown, path: string): MoneyResponse {
  const record = expectRecord(value, path);
  return {
    amount: readNumber(record, 'amount', path),
    currency: readString(record, 'currency', path),
  };
}

/**
 * Parses a payment.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed payment.
 */
export function parsePayment(value: unknown, path: string): PaymentResponse {
  const record = expectRecord(value, path);
  return {
    id: readString(record, 'id', path),
    amount: parseMoney(record.amount, `${path}.amount`),
    method: readLiteral(record, 'method', PAYMENT_METHODS, path),
    channel: readLiteral(record, 'channel', CHANNELS, path),
    status: readLiteral(record, 'status', PAYMENT_STATUSES, path),
    recordedAt: readString(record, 'recordedAt', path),
  };
}

/**
 * Parses an optional contract signature.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed signature, or null.
 */
export function parseContract(value: unknown, path: string): ContractResponse | null {
  if (value === null || value === undefined) {
    return null;
  }

  const record = expectRecord(value, path);
  return {
    signerName: readString(record, 'signerName', path),
    templateVersion: readString(record, 'templateVersion', path),
    channel: readLiteral(record, 'channel', CHANNELS, path),
    signedAt: readString(record, 'signedAt', path),
  };
}

/**
 * Parses a booking response, validating every field against the backend contract.
 * @param value Unknown response body.
 * @returns The typed booking.
 */
export function parseBooking(value: unknown): BookingResponse {
  const path = 'booking';
  const record = expectRecord(value, path);

  const allowedActions: BookingAction[] = readArray(record, 'allowedActions', path).map((action, index) =>
    readLiteral({ action }, 'action', BOOKING_ACTIONS, `${path}.allowedActions[${index}]`),
  );

  return {
    id: readString(record, 'id', path),
    photographerId: readString(record, 'photographerId', path),
    clientName: readString(record, 'clientName', path),
    clientPhone: readString(record, 'clientPhone', path),
    packageName: readString(record, 'packageName', path),
    status: readLiteral(record, 'status', BOOKING_STATUSES, path),
    sessionStart: readString(record, 'sessionStart', path),
    sessionEnd: readString(record, 'sessionEnd', path),
    expiresAt: readNullableString(record, 'expiresAt', path),
    packagePrice: parseMoney(record.packagePrice, `${path}.packagePrice`),
    depositRequired: parseMoney(record.depositRequired, `${path}.depositRequired`),
    totalPaid: parseMoney(record.totalPaid, `${path}.totalPaid`),
    balance: parseMoney(record.balance, `${path}.balance`),
    contract: parseContract(record.contract, `${path}.contract`),
    payments: readArray(record, 'payments', path).map((payment, index) =>
      parsePayment(payment, `${path}.payments[${index}]`),
    ),
    allowedActions,
  };
}

/**
 * Parses a compact booking of a list.
 * @param value Unknown element of the response.
 * @param path Location of the element, for error messages.
 * @returns The typed summary.
 */
export function parseBookingSummary(value: unknown, path: string): BookingSummaryResponse {
  const record = expectRecord(value, path);
  return {
    id: readString(record, 'id', path),
    clientName: readString(record, 'clientName', path),
    packageName: readString(record, 'packageName', path),
    status: readLiteral(record, 'status', BOOKING_STATUSES, path),
    sessionStart: readString(record, 'sessionStart', path),
    sessionEnd: readString(record, 'sessionEnd', path),
    packagePrice: parseMoney(record.packagePrice, `${path}.packagePrice`),
    balance: parseMoney(record.balance, `${path}.balance`),
  };
}

/**
 * Parses the list of compact bookings.
 * @param value Unknown response body.
 * @returns The typed summaries, in the order the backend sent them.
 */
export function parseBookingSummaries(value: unknown): readonly BookingSummaryResponse[] {
  return expectArray(value, 'bookings').map((item, index) => parseBookingSummary(item, `bookings[${index}]`));
}

/** Booking endpoints used by the photographer app. */
export interface BookingsApi {
  /**
   * Creates a tentative booking.
   * @param request Booking data.
   * @param signal Optional signal to cancel the request.
   * @returns The created booking.
   */
  createBooking(request: CreateBookingRequest, signal?: AbortSignal): Promise<BookingResponse>;

  /**
   * Lists the photographer's bookings, earliest session first.
   * @param photographerId Photographer (tenant) identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The booking summaries.
   */
  listBookings(photographerId: string, signal?: AbortSignal): Promise<readonly BookingSummaryResponse[]>;

  /**
   * Reads a booking.
   * @param id Booking identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The booking.
   */
  getBooking(id: string, signal?: AbortSignal): Promise<BookingResponse>;
}

/**
 * Creates the bookings API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The bookings API.
 */
export function createBookingsApi(client: HttpClient): BookingsApi {
  return {
    createBooking: (request, signal) => client.post('/api/bookings', request, parseBooking, signal),
    listBookings: (photographerId, signal) =>
      client.get(`/api/bookings?photographerId=${encodeURIComponent(photographerId)}`, parseBookingSummaries, signal),
    getBooking: (id, signal) => client.get(`/api/bookings/${encodeURIComponent(id)}`, parseBooking, signal),
  };
}
