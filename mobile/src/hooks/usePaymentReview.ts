import { Alert } from 'react-native';
import { openRejectPayment } from '../navigation/appNavigation';
import type { BookingResponse, PaymentResponse } from '../types/api/booking';
import type { PaymentReviewState } from '../types/hooks/usePaymentReview.types';
import { bookingCommandErrorMessage } from '../utils/apiErrors';
import { paymentVerificationConfirmation } from '../utils/bookingActions';
import { formatMoney } from '../utils/format';
import { useBookingCommand } from './useBookingCommand';

/**
 * Handlers for the payment proofs a client sent through the link. Verifying asks for confirmation first, because it counts
 * the money as received; rejecting opens its own form, since the reason is mandatory.
 * @param booking Booking the proofs belong to.
 * @returns The handlers and the state of the request.
 */
export function usePaymentReview(booking: BookingResponse): PaymentReviewState {
  const command = useBookingCommand(booking.id);

  return {
    isPending: command.isPending,
    errorMessage: command.isError ? bookingCommandErrorMessage(command.error) : null,
    verify: (payment: PaymentResponse) => {
      const texts = paymentVerificationConfirmation(booking.clientName, formatMoney(payment.amount));
      Alert.alert(texts.title, texts.message, [
        { text: 'Volver', style: 'cancel' },
        { text: texts.confirmLabel, onPress: () => command.mutate({ kind: 'verifyPayment', paymentId: payment.id }) },
      ]);
    },
    reject: (payment: PaymentResponse) => openRejectPayment(booking.id, payment.id),
  };
}
