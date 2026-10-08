import { BOOKING_ACTIONS, BOOKING_STATUSES, CHANNELS, PAYMENT_METHODS, PAYMENT_STATUSES } from '../constants/booking';
import type {
  BookingAction,
  BookingResponse,
  BookingSummaryResponse,
  ContractResponse,
  MoneyResponse,
  PaymentResponse,
} from '../types/api/booking';
import type { BookingsApi } from '../types/api/bookingsApi';
import type { HttpClient } from '../types/api/http';
import {
  expectArray,
  expectRecord,
  readArray,
  readLiteral,
  readNullableString,
  readNumber,
  readString,
} from './guards';

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

/**
 * Creates the bookings API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The bookings API.
 */
export function createBookingsApi(client: HttpClient): BookingsApi {
  return {
    createBooking: (request, signal) => client.post('/api/bookings', request, parseBooking, signal),
    rescheduleBooking: (id, request, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/reschedule`, request, parseBooking, signal),
    cancelBooking: (id, request, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/cancel`, request, parseBooking, signal),
    completeBooking: (id, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/complete`, undefined, parseBooking, signal),
    markClientAbsent: (id, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/client-absent`, undefined, parseBooking, signal),
    revertClientAbsent: (id, request, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/client-absent/revert`, request, parseBooking, signal),
    listBookings: (signal) => client.get('/api/bookings', parseBookingSummaries, signal),
    getBooking: (id, signal) => client.get(`/api/bookings/${encodeURIComponent(id)}`, parseBooking, signal),
  };
}
