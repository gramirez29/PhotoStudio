/**
 * TypeScript mirror of the backend response DTOs (PhotoStudio.Application.Bookings.Responses).
 * Enums travel as strings because the API uses JsonStringEnumConverter; property names are camelCase.
 * Keep in sync with the backend, or replace with types generated from /openapi/v1.json.
 */

/** Every value of the backend `BookingStatus` enum. */
export const BOOKING_STATUSES = ['Tentative', 'Confirmed', 'Completed', 'Cancelled', 'Expired', 'ClientAbsent'] as const;

/** Lifecycle status of a booking. */
export type BookingStatus = (typeof BOOKING_STATUSES)[number];

/** Every value of the backend `PaymentMethod` enum. */
export const PAYMENT_METHODS = ['SinpeMovil', 'Cash', 'Card'] as const;

/** Method used to pay. */
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

/** Every value of the backend `PaymentStatus` enum. */
export const PAYMENT_STATUSES = ['PendingVerification', 'Verified', 'Rejected'] as const;

/** Verification status of a payment. */
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

/** Every value of the backend `Channel` enum. */
export const CHANNELS = ['Portal', 'PhotographerApp', 'InPerson', 'External'] as const;

/** Channel through which an action was performed. */
export type Channel = (typeof CHANNELS)[number];

/** Every value of the backend `BookingAction` enum. */
export const BOOKING_ACTIONS = [
  'SignContract',
  'SubmitPaymentProof',
  'VerifyPayment',
  'RecordInPersonPayment',
  'Reschedule',
  'Cancel',
  'Complete',
  'MarkClientAbsent',
  'RevertClientAbsent',
] as const;

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
