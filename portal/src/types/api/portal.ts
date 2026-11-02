/**
 * TypeScript mirror of the response DTOs of the portal endpoints (PhotoStudio.Application.Portal). Names are camelCase, enums
 * travel as strings and dates as ISO 8601. Keep in sync with the backend, or replace with types generated from /openapi/v1.json.
 */
import type { BOOKING_STATUSES, IMAGE_USES, PAYMENT_STATUSES } from '../../constants/booking';
import type { GalleryStatus } from './studio';

/** Lifecycle status of a booking. */
export type BookingStatus = (typeof BOOKING_STATUSES)[number];

/** What the client allows to do with the photos of the session. */
export type ImageUse = (typeof IMAGE_USES)[number];

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

/** Extra sold with the booking, as the client sees it (backend `PortalExtraResponse`). */
export interface PortalExtra {
  /** Name of the extra. */
  readonly name: string;
  /** Number of units. */
  readonly quantity: number;
  /** Price of every unit together. */
  readonly total: Money;
}

/** Confirmation that a verification code was sent (backend `PortalCodeSentResponse`). */
export interface PortalCodeSent {
  /** Address the code went to, partly hidden, for example `m***@gmail.com`. */
  readonly maskedEmail: string;
  /** Instant the code stops being accepted (ISO 8601). */
  readonly expiresAt: string;
}

/** Session of a client who verified a code (backend `PortalSessionResponse`). It opens one booking only. */
export interface PortalSession {
  /** Short-lived token sent as `Authorization: Bearer`. */
  readonly accessToken: string;
  /** Instant the token stops being accepted (ISO 8601). */
  readonly expiresAt: string;
}

/** Body to report a SINPE Móvil transfer (backend `SubmitProofRequest`). */
export interface SubmitProofRequest {
  /** Amount transferred, in whole units of the currency of the booking. */
  readonly amount: number;
  /** Reference (confirmation number) of the transfer; omitted when the client gave none. */
  readonly reference?: string;
  /** Key that makes repeating the request safe: the same key registers the proof once. */
  readonly idempotencyKey: string;
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
  /** What the client pays: the package plus the extras minus the discount. */
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
  /** Whether the client can sign the contract now: it is unsigned, the booking waits for confirmation and there is an email for the code. */
  readonly canSignContract: boolean;
  /** Price of the package alone, before extras and discount. */
  readonly basePrice: Money;
  /** Extras sold with the booking; empty when there are none. */
  readonly extras: readonly PortalExtra[];
  /** Discount given to the client; zero when there is none. */
  readonly discount: Money;
}

/** Contract of a booking as the client reads it (backend `PortalContractResponse`). */
export interface PortalContract {
  /** Version of the template; it is sent back when signing so the client signs what they read. */
  readonly templateVersion: string;
  /** Text of the contract. Before signing, what the client still has to declare is shown as blanks; after signing it is the text that was signed. */
  readonly text: string;
  /** Whether the contract is already signed. */
  readonly signed: boolean;
}

/** Body to sign the contract (backend `PortalSignContractRequest`). */
export interface SignContractRequest {
  /** Version of the contract the client read. */
  readonly templateVersion: string;
  /** Name typed by the signer. */
  readonly signerName: string;
  /** Identity number of the signer, as typed (the server keeps the digits). */
  readonly idNumber: string;
  /** Number of people in the session. */
  readonly participantCount: number;
  /** Place of the session; omitted when the client left it blank. */
  readonly sessionLocation?: string;
  /** What the client allows to do with the photos. */
  readonly imageUse: ImageUse;
  /** Whether the client ticked the box that says they read and accept the contract. */
  readonly acceptsTerms: boolean;
}

/** What the link alone says about the gallery of a booking (backend `PortalGallerySummaryResponse`). */
export interface PortalGallerySummary {
  /** Where the gallery is. A gallery still waiting for its proofs is not reported at all (the API answers 404). */
  readonly status: GalleryStatus;
}

/** A proof the client can look at (backend `PortalProofResponse`). */
export interface PortalProof {
  /** Photo identifier, sent back to choose it. */
  readonly id: string;
  /** Signed address to show the proof. It is a secret that expires: it is never logged. */
  readonly url: string;
  /** Instant the address stops working (ISO 8601). */
  readonly expiresAt: string;
}

/** The selection a client already sent (backend `PortalSelectionResponse`). */
export interface PortalSelection {
  /** The photos chosen, in the order they were chosen. */
  readonly photoIds: readonly string[];
  /** Instant it was sent (ISO 8601). */
  readonly submittedAt: string;
  /** How many of the photos go beyond what the package includes. */
  readonly extraPhotos: number;
  /** What those extra photos cost. */
  readonly extraTotal: Money;
}

/** The gallery as its client sees it (backend `PortalGalleryResponse`). */
export interface PortalGallery {
  /** Where the gallery is: choosing, or already sent. */
  readonly status: GalleryStatus;
  /** Photos the client can choose without paying more. */
  readonly selectablePhotos: number;
  /** Price of each photo beyond those. */
  readonly extraPhotoPrice: Money;
  /** The proofs. */
  readonly photos: readonly PortalProof[];
  /** The selection that was sent, or null while the client has not sent one. */
  readonly selection: PortalSelection | null;
}
