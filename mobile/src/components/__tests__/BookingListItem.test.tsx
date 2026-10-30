import { fireEvent, render, screen } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { BOOKING_STATUSES } from '../../constants/booking';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import type { BookingSummaryResponse } from '../../types/api/booking';
import { STATUS_COLOR_ROLES } from '../../utils/statusColors';
import { BookingListItem } from '../BookingListItem';

/**
 * Builds the sample summary of a booking with a status.
 * @param status Status of the booking.
 * @returns The summary.
 */
function summary(status: BookingSummaryResponse['status']): BookingSummaryResponse {
  const booking = parseBooking({ ...bookingPayload, status });
  return {
    id: booking.id,
    clientName: booking.clientName,
    packageName: booking.packageName,
    status: booking.status,
    sessionStart: booking.sessionStart,
    sessionEnd: booking.sessionEnd,
    packagePrice: booking.packagePrice,
    balance: booking.balance,
  };
}

describe('BookingListItem', () => {
  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('shows the client, the package, the status and the balance', () => {
    render(<BookingListItem booking={summary('Confirmed')} onPress={jest.fn()} />);

    expect(screen.getByText('María Pérez')).toBeTruthy();
    expect(screen.getByText('Retrato familiar')).toBeTruthy();
    expect(screen.getByText('Confirmada')).toBeTruthy();
    expect(screen.getByText(/Saldo:/)).toBeTruthy();
  });

  it('opens the booking when it is tapped', () => {
    const onPress = jest.fn();
    const booking = summary('Confirmed');
    render(<BookingListItem booking={booking} onPress={onPress} />);

    fireEvent.press(screen.getByRole('button', { name: 'Reserva de María Pérez' }));

    expect(onPress).toHaveBeenCalledWith(booking.id);
  });

  it.each(BOOKING_STATUSES)('marks a %s booking with a strip of its color at the left of the card', (status) => {
    render(<BookingListItem booking={summary(status)} onPress={jest.fn()} />);

    const card = screen.getByRole('button', { name: 'Reserva de María Pérez' });
    const style = StyleSheet.flatten(card.props.style);
    expect(style.borderLeftWidth).toBeGreaterThanOrEqual(4);
    expect(style.borderLeftColor).toBe(LIGHT_PALETTE[STATUS_COLOR_ROLES[status]]);
  });

  it('takes the color of the strip from the theme, so it changes in the dark theme', () => {
    useAppearanceStore.setState({ preference: 'dark' });

    render(<BookingListItem booking={summary('Confirmed')} onPress={jest.fn()} />);

    const card = screen.getByRole('button', { name: 'Reserva de María Pérez' });
    expect(StyleSheet.flatten(card.props.style).borderLeftColor).toBe(DARK_PALETTE.success);
  });
});
