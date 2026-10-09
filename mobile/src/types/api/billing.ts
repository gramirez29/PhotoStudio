import type { REFUND_STATUSES, RETENTION_STATUSES, SETTLEMENT_REASONS } from '../../constants/billing';
import type { IN_PERSON_PAYMENT_METHODS } from '../../constants/payments';
import type { MoneyResponse, PaymentMethod } from './booking';

/** Why money is kept or returned after a booking ended without delivering its session. */
export type SettlementReason = (typeof SETTLEMENT_REASONS)[number];

/** State of the part of the deposit the photographer keeps. */
export type RetentionStatus = (typeof RETENTION_STATUSES)[number];

/** State of the money owed back to the client. */
export type RefundStatus = (typeof REFUND_STATUSES)[number];

/** The money side of a booking that ended (backend `SettlementResponse`). */
export interface SettlementResponse {
  /** Settlement identifier (GUID). */
  readonly id: string;
  /** Booking it is about (GUID). */
  readonly bookingId: string;
  /** Name of the client. */
  readonly clientName: string;
  /** Phone of the client. */
  readonly clientPhone: string;
  /** Name of the booked package. */
  readonly packageName: string;
  /** Start of the session (ISO 8601). */
  readonly sessionStart: string;
  /** Why money is kept or returned. */
  readonly reason: SettlementReason;
  /** Sum of the verified payments. */
  readonly totalPaid: MoneyResponse;
  /** Part of the deposit the photographer keeps. */
  readonly retained: MoneyResponse;
  /** State of the retention. */
  readonly retentionStatus: RetentionStatus;
  /** Amount that goes back to the client. */
  readonly refundAmount: MoneyResponse;
  /** State of the refund. */
  readonly refundStatus: RefundStatus;
  /** How the money was given back, once the refund is completed. */
  readonly refundMethod: PaymentMethod | null;
  /** Note left when the refund was completed, if any. */
  readonly refundNote: string | null;
  /** Instant the refund was completed (ISO 8601), or null. */
  readonly refundCompletedAt: string | null;
  /** Instant the settlement was opened (ISO 8601). */
  readonly createdAt: string;
}

/** The refunds the photographer still has to give back (backend `RefundListResponse`). */
export interface RefundListResponse {
  /** Settlements with a pending refund, the oldest first. */
  readonly items: readonly SettlementResponse[];
  /** How many refunds are pending (not only the ones listed). */
  readonly pendingCount: number;
}

/** Body to record that a refund was given back (backend `CompleteRefundRequest`). */
export interface CompleteRefundRequest {
  /** How the money was given back: cash or SINPE Móvil. */
  readonly method: (typeof IN_PERSON_PAYMENT_METHODS)[number];
  /** Optional note, for example the SINPE reference. */
  readonly note?: string;
}
