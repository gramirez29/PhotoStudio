import { fireEvent, render, screen } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { usePaymentReview } from '../../hooks/usePaymentReview';
import { useShareLink } from '../../hooks/useShareLink';
import type { BookingResponse } from '../../types/api/booking';
import type { PaymentReviewState } from '../../types/hooks/usePaymentReview.types';
import type { ShareLinkState } from '../../types/hooks/useShareLink.types';
import { PendingPaymentsCard } from '../PendingPaymentsCard';
import { ShareLinkCard } from '../ShareLinkCard';

jest.mock('../../hooks/usePaymentReview');
jest.mock('../../hooks/useShareLink');

/**
 * Builds the sample booking with a proof waiting for verification.
 * @param overrides Values to replace.
 * @returns The booking.
 */
function bookingWithProof(overrides: Partial<BookingResponse> = {}): BookingResponse {
  return {
    ...parseBooking({
      ...bookingPayload,
      payments: [{ ...bookingPayload.payments[0], status: 'PendingVerification', reference: 'SINPE-998877' }],
    }),
    allowedActions: ['VerifyPayment', 'Cancel'],
    ...overrides,
  };
}

/**
 * Builds the review state with every handler mocked.
 * @param overrides Values to replace.
 * @returns The state.
 */
function reviewState(overrides: Partial<PaymentReviewState> = {}): PaymentReviewState {
  return { isPending: false, errorMessage: null, verify: jest.fn(), reject: jest.fn(), ...overrides };
}

/**
 * Builds the share state with the action mocked.
 * @param overrides Values to replace.
 * @returns The state.
 */
function shareState(overrides: Partial<ShareLinkState> = {}): ShareLinkState {
  return { isPending: false, errorMessage: null, share: jest.fn(), ...overrides };
}

describe('PendingPaymentsCard', () => {
  it('shows the amount and the reference of each proof waiting for verification', () => {
    jest.mocked(usePaymentReview).mockReturnValue(reviewState());

    render(<PendingPaymentsCard booking={bookingWithProof()} />);

    expect(screen.getByText('Comprobantes por verificar')).toBeTruthy();
    expect(screen.getByText('Referencia: SINPE-998877')).toBeTruthy();
  });

  it('says there is no reference when the client gave none', () => {
    jest.mocked(usePaymentReview).mockReturnValue(reviewState());
    const booking = bookingWithProof();
    const withoutReference = { ...booking, payments: booking.payments.map((payment) => ({ ...payment, reference: null })) };

    render(<PendingPaymentsCard booking={withoutReference} />);

    expect(screen.getByText('Sin referencia')).toBeTruthy();
  });

  it('runs the handler of the button that is tapped, with the proof', () => {
    const state = reviewState();
    jest.mocked(usePaymentReview).mockReturnValue(state);
    const booking = bookingWithProof();
    render(<PendingPaymentsCard booking={booking} />);

    fireEvent.press(screen.getByText('Verificar pago'));
    fireEvent.press(screen.getByText('Rechazar comprobante'));

    expect(state.verify).toHaveBeenCalledWith(booking.payments[0]);
    expect(state.reject).toHaveBeenCalledWith(booking.payments[0]);
  });

  it('is hidden when the API does not allow verifying, even with a proof on record', () => {
    jest.mocked(usePaymentReview).mockReturnValue(reviewState());

    render(<PendingPaymentsCard booking={bookingWithProof({ allowedActions: ['Cancel'] })} />);

    expect(screen.queryByText('Comprobantes por verificar')).toBeNull();
  });

  it('is hidden when every proof was already resolved', () => {
    jest.mocked(usePaymentReview).mockReturnValue(reviewState());

    render(<PendingPaymentsCard booking={bookingWithProof({ payments: parseBooking(bookingPayload).payments })} />);

    expect(screen.queryByText('Comprobantes por verificar')).toBeNull();
  });

  it('shows the error of a failed verification', () => {
    jest.mocked(usePaymentReview).mockReturnValue(reviewState({ errorMessage: 'No se pudo completar la acción.' }));

    render(<PendingPaymentsCard booking={bookingWithProof()} />);

    expect(screen.getByText('No se pudo completar la acción.')).toBeTruthy();
  });
});

describe('ShareLinkCard', () => {
  it('offers to send the link and runs the action', () => {
    const state = shareState();
    jest.mocked(useShareLink).mockReturnValue(state);
    render(<ShareLinkCard booking={{ ...parseBooking(bookingPayload), clientEmail: 'maria@example.com' }} />);

    fireEvent.press(screen.getByText('Enviar enlace por WhatsApp'));

    expect(state.share).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/maria@example.com/)).toBeTruthy();
  });

  it('warns that the client cannot upload proofs when the booking has no email', () => {
    jest.mocked(useShareLink).mockReturnValue(shareState());

    render(<ShareLinkCard booking={parseBooking(bookingPayload)} />);

    expect(screen.getByText(/no tiene correo del cliente/)).toBeTruthy();
  });

  it('is hidden once the booking is closed', () => {
    jest.mocked(useShareLink).mockReturnValue(shareState());

    render(<ShareLinkCard booking={{ ...parseBooking(bookingPayload), status: 'Cancelled' }} />);

    expect(screen.queryByText('Enlace del cliente')).toBeNull();
  });

  it('shows why sharing failed', () => {
    jest.mocked(useShareLink).mockReturnValue(shareState({ errorMessage: 'No se pudo abrir WhatsApp. Inténtalo de nuevo.' }));

    render(<ShareLinkCard booking={parseBooking(bookingPayload)} />);

    expect(screen.getByText('No se pudo abrir WhatsApp. Inténtalo de nuevo.')).toBeTruthy();
  });
});
