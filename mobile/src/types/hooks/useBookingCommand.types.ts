import type { RecordInPersonPaymentRequest, SignContractInPersonRequest } from '../api/booking';

/** An action on an existing booking, ready to be sent to the API. */
export type BookingCommand =
  | { readonly kind: 'signContract'; readonly request: SignContractInPersonRequest }
  | { readonly kind: 'recordPayment'; readonly request: RecordInPersonPaymentRequest }
  | { readonly kind: 'cancel'; readonly reason: string | null }
  | { readonly kind: 'complete' }
  | { readonly kind: 'markClientAbsent' }
  | { readonly kind: 'revertClientAbsent'; readonly reason: string };
