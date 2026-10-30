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
  IMAGE_USES,
  PACKAGE_TIERS,
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
  /** Reference (confirmation number) the client gave with a SINPE Móvil proof, or null. */
  readonly reference: string | null;
}

/** What the client allowed to do with the photos of the session. */
export type ImageUse = (typeof IMAGE_USES)[number];

/** Package the client booked (backend `PackageTier`). */
export type PackageTier = (typeof PACKAGE_TIERS)[number];

/** Extra sold with a booking (backend `BookingExtraResponse`). */
export interface BookingExtraResponse {
  /** Stable code of the extra. */
  readonly code: string;
  /** Name of the extra. */
  readonly name: string;
  /** Price of one unit when the booking was made. */
  readonly unitPrice: MoneyResponse;
  /** Number of units. */
  readonly quantity: number;
  /** Price of every unit together. */
  readonly total: MoneyResponse;
}

/** What the client declared when signing the contract through the portal (backend `ClientDeclarationResponse`). */
export interface ClientDeclarationResponse {
  /** Identity number of the signer, digits only (cédula of 9 digits or DIMEX of 11 or 12). */
  readonly idNumber: string;
  /** Number of people in the session. */
  readonly participantCount: number;
  /** Place of the session, or null when the client left it blank. */
  readonly sessionLocation: string | null;
  /** What the client allowed to do with the photos. */
  readonly imageUse: ImageUse;
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
  /** What the client declared; null when the photographer registered the signature (in person or on paper). */
  readonly declaration: ClientDeclarationResponse | null;
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
  /** Name of the session (family portrait, pre-wedding...). */
  readonly packageName: string;
  /** Current status. */
  readonly status: BookingStatus;
  /** Session start (ISO 8601). */
  readonly sessionStart: string;
  /** Session end (ISO 8601). */
  readonly sessionEnd: string;
  /** End of the tentative hold (ISO 8601), or null. */
  readonly expiresAt: string | null;
  /** What the client pays: the package plus the extras minus the discount. */
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
  /** Package the client booked, or null for bookings made before it was recorded. */
  readonly packageTier: PackageTier | null;
  /** Price of the package alone, before extras and discount. */
  readonly basePrice: MoneyResponse;
  /** Extras sold with the booking. */
  readonly extras: readonly BookingExtraResponse[];
  /** Discount given to the client; zero when there is none. */
  readonly discount: MoneyResponse;
  /** Actions the caller can perform right now. */
  readonly allowedActions: readonly BookingAction[];
  /** Client email, or null when the booking has none. The client portal sends its verification codes there. */
  readonly clientEmail: string | null;
}

/** Link the client opens to see the booking (backend `BookingLinkResponse`). */
export interface BookingLinkResponse {
  /** Secret token of the link; shown only when the link is created. */
  readonly token: string;
  /** Full URL to send to the client. */
  readonly url: string;
  /** Instant the link stops working (ISO 8601). */
  readonly expiresAt: string;
}

/** Body to reject a payment proof (backend `RejectPaymentRequest`). */
export interface RejectPaymentRequest {
  /** Why the proof is rejected; required. */
  readonly reason: string;
}

/** One page of bookings (backend `BookingListResponse`), the most recently created first. */
export interface BookingListResponse {
  /** The bookings of this page. */
  readonly items: readonly BookingSummaryResponse[];
  /** What to send back to get the next page, or null when this is the last one. */
  readonly nextCursor: string | null;
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
  /** Client email, optional. Without it the client can see the booking through the link but cannot act on it. */
  readonly clientEmail?: string;
  /** Name of the session (family portrait, pre-wedding...). */
  readonly packageName: string;
  /** Price of the package alone, before extras and discount. */
  readonly packagePrice: number;
  /** ISO 4217 currency code. */
  readonly currency: string;
  /** Session start (ISO 8601). */
  readonly sessionStart: string;
  /** Session end (ISO 8601). */
  readonly sessionEnd: string;
  /** Package the client booked. */
  readonly packageTier?: PackageTier;
  /** Extras sold with the booking; omitted when there are none. */
  readonly extras?: readonly BookingExtraRequest[];
  /** Discount given to the client in colones; omitted when there is none. */
  readonly discount?: number;
}

/** Extra sold with a booking, as sent when it is created (backend `BookingExtraRequest`). */
export interface BookingExtraRequest {
  /** Stable code of the extra. */
  readonly code: string;
  /** Name of the extra. */
  readonly name: string;
  /** Price of one unit when the booking is made. */
  readonly unitPrice: number;
  /** Number of units. */
  readonly quantity: number;
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
  /** What the client declared when signing on the phone; omitted for a contract on paper. */
  readonly declaration?: InPersonDeclarationRequest;
}

/** What the client declared when signing the contract on the photographer's phone (backend `InPersonDeclarationRequest`). */
export interface InPersonDeclarationRequest {
  /** Identity number of the signer: a cédula of 9 digits or a DIMEX of 11 or 12. */
  readonly idNumber: string;
  /** Number of people in the session. */
  readonly participantCount: number;
  /** Place of the session; omitted when the client left it blank. */
  readonly sessionLocation?: string;
  /** What the client allows to do with the photos. */
  readonly imageUse: ImageUse;
}

/** Contract of a booking as the photographer reads it (backend `BookingContractResponse`). */
export interface BookingContractResponse {
  /** Version of the template; it is sent back when signing so the client signs what they read. */
  readonly templateVersion: string;
  /** Text of the contract. Before signing, what the client still has to declare is shown as blanks; after signing it is the text that was signed, or empty for a contract on paper. */
  readonly text: string;
  /** Whether the contract is already signed. */
  readonly signed: boolean;
}

/** Body to correct the data of a booking (backend `EditBookingDetailsRequest`). It carries every editable value; what did not change is the same as before. */
export interface EditBookingDetailsRequest {
  /** Name of the client. */
  readonly clientName: string;
  /** Phone of the client, normalized (`+50670189220`). */
  readonly clientPhone: string;
  /** Email of the client; omitted when the booking has none. */
  readonly clientEmail?: string;
  /** Name of the package. */
  readonly packageName: string;
  /** Price of the package alone, in the currency of the booking. */
  readonly packagePrice: number;
  /** Package the client booked; omitted leaves it as it is. */
  readonly packageTier?: PackageTier;
  /** Extras as they should be now (an empty list removes them); omitted leaves them as they are. */
  readonly extras?: readonly BookingExtraRequest[];
  /** Discount in colones as it should be now (zero removes it); omitted leaves it as it is. */
  readonly discount?: number;
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
