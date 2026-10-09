import { fireEvent, render, screen } from '@testing-library/react-native';
import { parseSettlement } from '../../api/billingApi';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { pendingSettlementPayload } from '../../api/__fixtures__/billingPayloads';
import { useRefunds } from '../../hooks/useRefunds';
import { useSettlement } from '../../hooks/useSettlement';
import { openRefund } from '../../navigation/appNavigation';
import { RefundListItem } from '../RefundListItem';
import { RefundsButton } from '../RefundsButton';
import { SettlementCard } from '../SettlementCard';

jest.mock('../../hooks/useRefunds');
jest.mock('../../hooks/useSettlement');
jest.mock('../../navigation/appNavigation');

const settlement = parseSettlement(pendingSettlementPayload);
const booking = parseBooking(bookingPayload);

/**
 * Makes the settlement query return a value.
 * @param data Settlement, or null for "nothing to settle".
 */
function settlementIs(data: ReturnType<typeof parseSettlement> | null): void {
  jest.mocked(useSettlement).mockReturnValue({ data } as ReturnType<typeof useSettlement>);
}

describe('RefundsButton', () => {
  it('shows nothing while no refund is pending', () => {
    jest.mocked(useRefunds).mockReturnValue({ data: { items: [], pendingCount: 0 } } as unknown as ReturnType<typeof useRefunds>);

    render(<RefundsButton />);

    expect(screen.queryByRole('button')).toBeNull();
  });

  it('shows the count and opens the list', () => {
    jest.mocked(useRefunds).mockReturnValue({ data: { items: [], pendingCount: 2 } } as unknown as ReturnType<typeof useRefunds>);

    render(<RefundsButton />);

    expect(screen.getByRole('button', { name: 'Reembolsos pendientes (2)' })).toBeTruthy();
  });
});

describe('RefundListItem', () => {
  it('shows who is owed how much and runs each action', () => {
    const handlers = { onContact: jest.fn(), onComplete: jest.fn(), onOpenBooking: jest.fn() };
    render(<RefundListItem settlement={settlement} {...handlers} />);

    expect(screen.getByText('María Pérez Solano')).toBeTruthy();
    expect(screen.getByText('Cancelaste la sesión')).toBeTruthy();

    fireEvent.press(screen.getByRole('button', { name: 'Marcar como devuelto' }));
    fireEvent.press(screen.getByRole('button', { name: 'Escribir por WhatsApp' }));
    fireEvent.press(screen.getByRole('button', { name: 'Ver reserva' }));

    expect(handlers.onComplete).toHaveBeenCalledWith(settlement);
    expect(handlers.onContact).toHaveBeenCalledWith(settlement);
    expect(handlers.onOpenBooking).toHaveBeenCalledWith(settlement);
  });

  it('mentions the deposit that stays with the photographer', () => {
    render(
      <RefundListItem
        settlement={{ ...settlement, retentionStatus: 'Applied', retained: { amount: 50000, currency: 'CRC' } }}
        onContact={jest.fn()}
        onComplete={jest.fn()}
        onOpenBooking={jest.fn()}
      />,
    );

    expect(screen.getByText(/Te quedas con ₡/)).toBeTruthy();
  });
});

describe('SettlementCard', () => {
  it('shows nothing for an active booking, without even asking', () => {
    settlementIs(null);

    render(<SettlementCard booking={{ ...booking, status: 'Confirmed' }} />);

    expect(screen.queryByText('Dinero de esta reserva')).toBeNull();
    expect(useSettlement).toHaveBeenCalledWith(booking.id, false);
  });

  it('shows nothing when the booking ended with nothing paid', () => {
    settlementIs(null);

    render(<SettlementCard booking={{ ...booking, status: 'Cancelled' }} />);

    expect(screen.queryByText('Dinero de esta reserva')).toBeNull();
  });

  it('shows what happened to the money and opens the refund form while it is pending', () => {
    settlementIs(settlement);
    render(<SettlementCard booking={{ ...booking, status: 'Cancelled' }} />);

    expect(screen.getByText('Dinero de esta reserva')).toBeTruthy();
    expect(screen.getByText(/Debes devolver ₡/)).toBeTruthy();

    fireEvent.press(screen.getByRole('button', { name: 'Marcar reembolso como devuelto' }));

    expect(openRefund).toHaveBeenCalledWith(booking.id);
  });

  it('offers no button once the refund was given back', () => {
    settlementIs({ ...settlement, refundStatus: 'Completed', refundMethod: 'Cash', refundCompletedAt: '2026-10-18T10:00:00+00:00' });

    render(<SettlementCard booking={{ ...booking, status: 'Cancelled' }} />);

    expect(screen.getByText(/Devuelto ₡/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Marcar reembolso como devuelto' })).toBeNull();
  });
});
