import type { BookingStatus, PaymentStatus } from '../types/api/portal';
import type { StatusTone } from '../types/utils/labels.types';

/** Label of each booking status, as the client reads it. */
export const BOOKING_STATUS_LABELS: Readonly<Record<BookingStatus, string>> = {
  Tentative: 'Por confirmar',
  Confirmed: 'Confirmada',
  Completed: 'Sesión realizada',
  Cancelled: 'Cancelada',
  Expired: 'Vencida',
  ClientAbsent: 'Sesión no realizada',
};

/** Tone of the dot of each booking status. */
export const BOOKING_STATUS_TONES: Readonly<Record<BookingStatus, StatusTone>> = {
  Tentative: 'warning',
  Confirmed: 'success',
  Completed: 'success',
  Cancelled: 'danger',
  Expired: 'danger',
  ClientAbsent: 'danger',
};

/** Label of each payment status, as the client reads it. */
export const PAYMENT_STATUS_LABELS: Readonly<Record<PaymentStatus, string>> = {
  PendingVerification: 'En revisión',
  Verified: 'Verificado',
  Rejected: 'No aceptado',
};

/** Tone of the dot of each payment status. */
export const PAYMENT_STATUS_TONES: Readonly<Record<PaymentStatus, StatusTone>> = {
  PendingVerification: 'warning',
  Verified: 'success',
  Rejected: 'danger',
};
