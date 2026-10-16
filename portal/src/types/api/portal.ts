/**
 * TypeScript mirror of the response DTOs of the portal endpoints (PhotoStudio.Application.Portal). Names are camelCase, enums
 * travel as strings and dates as ISO 8601. Keep in sync with the backend, or replace with types generated from /openapi/v1.json.
 */
import type { BOOKING_STATUSES, PAYMENT_STATUSES } from '../../constants/booking';

/** Lifecycle status of a booking. */
export type BookingStatus = (typeof BOOKING_STATUSES)[number];

/** Verification status of a payment. */
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

/** Monetary amount (backend `MoneyResponse`). */
export interface Money {
  /** Numeric amount. */
  readonly amount: number;
  /** ISO 4217 currency code. */
  readonly currency: string;
}

/** Payment as the client sees it (backend `PortalPaymentResponse`). */
export interface PortalPayment {
  /** Amount of the payment. */
  readonly amount: Money;
  /** Verification status. */
  readonly status: PaymentStatus;
  /** Instant the payment was recorded (ISO 8601). */
  readonly recordedAt: string;
}

/** Booking as the client sees it through the link (backend `PortalBookingResponse`). */
export interface PortalBooking {
  /** Name of the photographer. */
  readonly photographerName: string;
  /** Name of the client. */
  readonly clientName: string;
  /** Package name. */
  readonly packageName: string;
  /** Current status. */
  readonly status: BookingStatus;
  /** Session start (ISO 8601). */
  readonly sessionStart: string;
  /** Session end (ISO 8601). */
  readonly sessionEnd: string;
  /** End of the tentative hold (ISO 8601), or null. */
  readonly expiresAt: string | null;
  /** Package price. */
  readonly packagePrice: Money;
  /** Deposit required to confirm. */
  readonly depositRequired: Money;
  /** Sum of verified payments. */
  readonly totalPaid: Money;
  /** Outstanding balance. */
  readonly balance: Money;
  /** Whether the contract is signed. */
  readonly contractSigned: boolean;
  /** Payments recorded, with their state. */
  readonly payments: readonly PortalPayment[];
  /** Whether the client can submit a payment proof now. */
  readonly canSubmitProof: boolean;
  /** Whether the booking has an email to send verification codes to. */
  readonly canReceiveCode: boolean;
}
