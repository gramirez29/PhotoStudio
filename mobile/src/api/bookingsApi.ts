import {
  BOOKING_ACTIONS,
  BOOKING_STATUSES,
  CHANNELS,
  IMAGE_USES,
  PACKAGE_TIERS,
  PAYMENT_METHODS,
  PAYMENT_STATUSES,
} from '../constants/booking';
import type {
  BookingAction,
  BookingContractResponse,
  BookingExtraResponse,
  BookingLinkResponse,
  BookingListResponse,
  BookingResponse,
  BookingSummaryResponse,
  ClientDeclarationResponse,
  ContractResponse,
  MoneyResponse,
  PaymentResponse,
} from '../types/api/booking';
import type { BookingsApi } from '../types/api/bookingsApi';
import type { HttpClient } from '../types/api/http';
import { buildPagedPath } from '../utils/pagination';
import {
  expectRecord,
  readArray,
  readBoolean,
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
 * Parses an extra sold with a booking.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed extra.
 */
export function parseBookingExtra(value: unknown, path: string): BookingExtraResponse {
  const record = expectRecord(value, path);
  return {
    code: readString(record, 'code', path),
    name: readString(record, 'name', path),
    unitPrice: parseMoney(record.unitPrice, `${path}.unitPrice`),
    quantity: readNumber(record, 'quantity', path),
    total: parseMoney(record.total, `${path}.total`),
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
    reference: readNullableString(record, 'reference', path),
  };
}

/**
 * Parses the link created for a booking.
 * @param value Unknown response body.
 * @returns The typed link.
 */
export function parseBookingLink(value: unknown): BookingLinkResponse {
  const path = 'bookingLink';
  const record = expectRecord(value, path);
  return {
    token: readString(record, 'token', path),
    url: readString(record, 'url', path),
    expiresAt: readString(record, 'expiresAt', path),
  };
}

/**
 * Parses the contract of a booking.
 * @param value Unknown response body.
 * @returns The typed contract.
 */
export function parseBookingContract(value: unknown): BookingContractResponse {
  const path = 'contract';
  const record = expectRecord(value, path);
  return {
    templateVersion: readString(record, 'templateVersion', path),
    text: readString(record, 'text', path),
    signed: readBoolean(record, 'signed', path),
  };
}

/**
 * Parses an optional declaration of the client. A backend that does not send the field, or sends null, means the photographer
 * registered the signature.
 * @param value Unknown value from the response.
 * @param path Location of the value, for error messages.
 * @returns The typed declaration, or null.
 */
export function parseDeclaration(value: unknown, path: string): ClientDeclarationResponse | null {
  if (value === null || value === undefined) {
    return null;
  }

  const record = expectRecord(value, path);
  return {
    idNumber: readString(record, 'idNumber', path),
    participantCount: readNumber(record, 'participantCount', path),
    sessionLocation: readNullableString(record, 'sessionLocation', path),
    imageUse: readLiteral(record, 'imageUse', IMAGE_USES, path),
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
    declaration: parseDeclaration(record.declaration, `${path}.declaration`),
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
    clientEmail: readNullableString(record, 'clientEmail', path),
    packageTier: record.packageTier === null ? null : readLiteral(record, 'packageTier', PACKAGE_TIERS, path),
    basePrice: parseMoney(record.basePrice, `${path}.basePrice`),
    extras: readArray(record, 'extras', path).map((extra, index) => parseBookingExtra(extra, `${path}.extras[${index}]`)),
    discount: parseMoney(record.discount, `${path}.discount`),
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
 * Parses one page of compact bookings.
 * @param value Unknown response body.
 * @returns The typed page: the summaries in the order the backend sent them and the cursor of the next page.
 */
export function parseBookingList(value: unknown): BookingListResponse {
  const path = 'bookings';
  const record = expectRecord(value, path);
  return {
    items: readArray(record, 'items', path).map((item, index) => parseBookingSummary(item, `${path}.items[${index}]`)),
    nextCursor: readNullableString(record, 'nextCursor', path),
  };
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
    editBookingDetails: (id, request, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/details`, request, parseBooking, signal),
    signContractInPerson: (id, request, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/contract/in-person`, request, parseBooking, signal),
    recordInPersonPayment: (id, request, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/payments/in-person`, request, parseBooking, signal),
    createBookingLink: (id, signal) =>
      client.post(`/api/bookings/${encodeURIComponent(id)}/link`, undefined, parseBookingLink, signal),
    verifyPayment: (id, paymentId, signal) =>
      client.post(
        `/api/bookings/${encodeURIComponent(id)}/payments/${encodeURIComponent(paymentId)}/verify`,
        undefined,
        parseBooking,
        signal,
      ),
    rejectPayment: (id, paymentId, request, signal) =>
      client.post(
        `/api/bookings/${encodeURIComponent(id)}/payments/${encodeURIComponent(paymentId)}/reject`,
        request,
        parseBooking,
        signal,
      ),
    listBookings: (cursor, signal) => client.get(buildPagedPath('/api/bookings', cursor), parseBookingList, signal),
    getBooking: (id, signal) => client.get(`/api/bookings/${encodeURIComponent(id)}`, parseBooking, signal),
    getContract: (id, signal) => client.get(`/api/bookings/${encodeURIComponent(id)}/contract`, parseBookingContract, signal),
  };
}
