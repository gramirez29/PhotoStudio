import type { BookingAction, BookingStatus } from '../types/api/booking';

/** Spanish label of each booking status, as shown to the photographer. */
export const BOOKING_STATUS_LABELS: Readonly<Record<BookingStatus, string>> = {
  Tentative: 'Tentativa',
  Confirmed: 'Confirmada',
  Completed: 'Completada',
  Cancelled: 'Cancelada',
  Expired: 'Vencida',
  ClientAbsent: 'Cliente ausente',
};

/** Spanish label of each booking action. */
export const BOOKING_ACTION_LABELS: Readonly<Record<BookingAction, string>> = {
  SignContract: 'Firmar contrato',
  SubmitPaymentProof: 'Subir comprobante',
  VerifyPayment: 'Verificar pago',
  RecordInPersonPayment: 'Registrar pago presencial',
  Reschedule: 'Reprogramar',
  Cancel: 'Cancelar',
  Complete: 'Marcar sesión completada',
  MarkClientAbsent: 'Marcar cliente ausente',
  RevertClientAbsent: 'Revertir ausencia',
};
