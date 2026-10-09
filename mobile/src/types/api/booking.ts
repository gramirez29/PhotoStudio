/**
 * TypeScript mirror of the backend response DTOs (PhotoStudio.Application.Bookings.Responses).
 * Enums travel as strings because the API uses JsonStringEnumConverter; property names are camelCase.
 * Keep in sync with the backend, or replace with types generated from /openapi/v1.json.
 * Enum values live in `src/constants/booking.ts`; the enum types are derived from them here.
 */
import type {
  BOOKING_ACTIONS,
  BOOKING_STATUSES,
  CHANNELS,
  PAYMENT_METHODS,
  PAYMENT_STATUSES,
} from '../../constants/booking';
import type { IN_PERSON_PAYMENT_METHODS } from '../../constants/payments';

/** Lifecycle status of a booking. */
export type BookingStatus = (typeof BOOKING_STATUSES)[number];

/** Method used to pay. */
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

/** Verification status of a payment. */
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

/** Channel through which an action was performed. */
export type Channel = (typeof CHANNELS)[number];

/** Action the caller is allowed to perform on a booking right now. */
export type BookingAction = (typeof BOOKING_ACTIONS)[number];

/** Monetary amount (backend `MoneyResponse`). */
export interface MoneyResponse {
  /** Numeric amount. */
  readonly amount: number;
  /** ISO 4217 currency code. */
  readonly currency: string;
}

/** Payment (backend `PaymentResponse`). */
export interface PaymentResponse {
  /** Payment identifier (GUID). */
  readonly id: string;
  /** Paid amount. */
  readonly amount: MoneyResponse;
  /** Payment method. */
  readonly method: PaymentMethod;
  /** Channel of the payment. */
  readonly channel: Channel;
  /** Verification status. */
  readonly status: PaymentStatus;
  /** Instant the payment was recorded (ISO 8601). */
  readonly recordedAt: string;
}

/** Contract signature (backend `ContractResponse`). */
export interface ContractResponse {
  /** Name typed by the signer. */
  readonly signerName: string;
  /** Signed template version. */
  readonly templateVersion: string;
  /** Channel of the signature. */
  readonly channel: Channel;
  /** Instant of the signature (ISO 8601). */
  readonly signedAt: string;
}

/** Booking (backend `BookingResponse`). */
export interface BookingResponse {
  /** Booking identifier (GUID). */
  readonly id: string;
  /** Photographer identifier (GUID). */
  readonly photographerId: string;
  /** Client name. */
  readonly clientName: string;
  /** Client phone. */
  readonly clientPhone: string;
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
  readonly packagePrice: MoneyResponse;
  /** Deposit required to confirm. */
  readonly depositRequired: MoneyResponse;
  /** Sum of verified payments. */
  readonly totalPaid: MoneyResponse;
  /** Outstanding balance. */
  readonly balance: MoneyResponse;
  /** Contract signature, or null when not signed. */
  readonly contract: ContractResponse | null;
  /** Recorded payments. */
  readonly payments: readonly PaymentResponse[];
  /** Actions the caller can perform right now. */
  readonly allowedActions: readonly BookingAction[];
}

/** Compact booking used in lists (backend `BookingSummaryResponse`). */
export interface BookingSummaryResponse {
  /** Booking identifier (GUID). */
  readonly id: string;
  /** Client name. */
  readonly clientName: string;
  /** Package name. */
  readonly packageName: string;
  /** Current status. */
  readonly status: BookingStatus;
  /** Session start (ISO 8601). */
  readonly sessionStart: string;
  /** Session end (ISO 8601). */
  readonly sessionEnd: string;
  /** Package price. */
  readonly packagePrice: MoneyResponse;
  /** Outstanding balance. */
  readonly balance: MoneyResponse;
}

/** Body to create a booking (backend `CreateBookingRequest`). The photographer is not sent: the backend takes it from the access token. */
export interface CreateBookingRequest {
  /** Client name. */
  readonly clientName: string;
  /** Client phone, with country code. */
  readonly clientPhone: string;
  /** Package name. */
  readonly packageName: string;
  /** Package price. */
  readonly packagePrice: number;
  /** ISO 4217 currency code. */
  readonly currency: string;
  /** Session start (ISO 8601). */
  readonly sessionStart: string;
  /** Session end (ISO 8601). */
  readonly sessionEnd: string;
}

/** Body to move a confirmed booking to another slot (backend `RescheduleBookingRequest`). */
export interface RescheduleBookingRequest {
  /** New session start (ISO 8601). */
  readonly sessionStart: string;
  /** New session end (ISO 8601). */
  readonly sessionEnd: string;
}

/** Body to cancel a booking (backend `CancelBookingRequest`). Send `{}` when there is no reason. */
export interface CancelBookingRequest {
  /** Cancellation reason; required by the backend once the booking is confirmed. */
  readonly reason?: string;
}

/** Body to record the signature of a contract in person or on paper (backend `SignContractInPersonRequest`). */
export interface SignContractInPersonRequest {
  /** Name of the person who signs. */
  readonly signerName: string;
  /** Version of the contract template that was signed. */
  readonly templateVersion: string;
  /** True when the contract is a paper one (channel `External`), false when the client signs on the phone (`InPerson`). */
  readonly isPaperContract: boolean;
}

/** Body to record a payment received in person (backend `RecordInPersonPaymentRequest`). */
export interface RecordInPersonPaymentRequest {
  /** Amount received. */
  readonly amount: number;
  /** ISO 4217 currency code; the one of the booking. */
  readonly currency: string;
  /** Method used: cash or SINPE Móvil. */
  readonly method: (typeof IN_PERSON_PAYMENT_METHODS)[number];
  /** Key that makes repeating the request safe: the same key records the payment once. */
  readonly idempotencyKey: string;
}

/** Body to revert a client-absent mark (backend `RevertClientAbsentRequest`). */
export interface RevertClientAbsentRequest {
  /** Why the mark is reverted; required. */
  readonly reason: string;
}
