import { REFUND_STATUSES, RETENTION_STATUSES, SETTLEMENT_REASONS } from '../constants/billing';
import { PAYMENT_METHODS } from '../constants/booking';
import type { RefundListResponse, SettlementResponse } from '../types/api/billing';
import type { BillingApi } from '../types/api/billingApi';
import type { HttpClient } from '../types/api/http';
import { parseMoney } from './bookingsApi';
import { expectRecord, readArray, readLiteral, readNullableString, readNumber, readString } from './guards';

/**
 * Parses a settlement, validating every field against the backend contract.
 * @param value Unknown response body or list element.
 * @param path Location of the value, for error messages.
 * @returns The typed settlement.
 */
export function parseSettlement(value: unknown, path = 'settlement'): SettlementResponse {
  const record = expectRecord(value, path);
  const method = readNullableString(record, 'refundMethod', path);
  return {
    id: readString(record, 'id', path),
    bookingId: readString(record, 'bookingId', path),
    clientName: readString(record, 'clientName', path),
    clientPhone: readString(record, 'clientPhone', path),
    packageName: readString(record, 'packageName', path),
    sessionStart: readString(record, 'sessionStart', path),
    reason: readLiteral(record, 'reason', SETTLEMENT_REASONS, path),
    totalPaid: parseMoney(record.totalPaid, `${path}.totalPaid`),
    retained: parseMoney(record.retained, `${path}.retained`),
    retentionStatus: readLiteral(record, 'retentionStatus', RETENTION_STATUSES, path),
    refundAmount: parseMoney(record.refundAmount, `${path}.refundAmount`),
    refundStatus: readLiteral(record, 'refundStatus', REFUND_STATUSES, path),
    refundMethod: method === null ? null : readLiteral({ method }, 'method', PAYMENT_METHODS, `${path}.refundMethod`),
    refundNote: readNullableString(record, 'refundNote', path),
    refundCompletedAt: readNullableString(record, 'refundCompletedAt', path),
    createdAt: readString(record, 'createdAt', path),
  };
}

/**
 * Parses the list of pending refunds.
 * @param value Unknown response body.
 * @returns The typed list.
 */
export function parseRefundList(value: unknown): RefundListResponse {
  const path = 'refunds';
  const record = expectRecord(value, path);
  return {
    items: readArray(record, 'items', path).map((item, index) => parseSettlement(item, `${path}.items[${index}]`)),
    pendingCount: readNumber(record, 'pendingCount', path),
  };
}

/**
 * Creates the billing API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The billing API.
 */
export function createBillingApi(client: HttpClient): BillingApi {
  return {
    listRefunds: (signal) => client.get('/api/billing/refunds', parseRefundList, signal),
    getSettlement: (bookingId, signal) =>
      client.get(`/api/billing/settlements/${encodeURIComponent(bookingId)}`, (body) => parseSettlement(body), signal),
    completeRefund: (bookingId, request, signal) =>
      client.post(
        `/api/billing/settlements/${encodeURIComponent(bookingId)}/refund/complete`,
        request,
        (body) => parseSettlement(body),
        signal,
      ),
  };
}
