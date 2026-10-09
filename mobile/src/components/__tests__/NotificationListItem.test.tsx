import { fireEvent, render, screen } from '@testing-library/react-native';
import type { NotificationResponse } from '../../types/api/notifications';
import { NotificationListItem } from '../NotificationListItem';

/**
 * Builds a notification, unread unless overridden.
 * @param overrides Values to replace.
 * @returns The notification.
 */
function notification(overrides: Partial<NotificationResponse> = {}): NotificationResponse {
  return {
    id: 'n1',
    type: 'SessionReminder',
    bookingId: 'b1',
    clientName: 'María Pérez',
    clientPhone: '+50670189220',
    packageName: 'Retrato Familiar',
    sessionStart: '2026-10-17T21:00:00+00:00',
    balance: null,
    deliveredAt: '2026-10-16T21:00:00+00:00',
    readAt: null,
    ...overrides,
  };
}

describe('NotificationListItem', () => {
  it('shows an unread notice with its three actions', () => {
    const handlers = { onSendWhatsApp: jest.fn(), onOpenBooking: jest.fn(), onMarkRead: jest.fn() };
    render(<NotificationListItem notification={notification()} {...handlers} />);

    expect(screen.getByText('Sesión próxima')).toBeTruthy();
    expect(screen.getByLabelText('Sin leer')).toBeTruthy();

    fireEvent.press(screen.getByRole('button', { name: 'Enviar por WhatsApp' }));
    fireEvent.press(screen.getByRole('button', { name: 'Ver reserva' }));
    fireEvent.press(screen.getByRole('button', { name: 'Marcar como leído' }));

    expect(handlers.onSendWhatsApp).toHaveBeenCalledWith(expect.objectContaining({ id: 'n1' }));
    expect(handlers.onOpenBooking).toHaveBeenCalledWith(expect.objectContaining({ id: 'n1' }));
    expect(handlers.onMarkRead).toHaveBeenCalledWith(expect.objectContaining({ id: 'n1' }));
  });

  it('hides the unread marker and the mark-as-read action once it is read', () => {
    render(
      <NotificationListItem
        notification={notification({ readAt: '2026-10-17T00:00:00+00:00' })}
        onSendWhatsApp={jest.fn()}
        onOpenBooking={jest.fn()}
        onMarkRead={jest.fn()}
      />,
    );

    expect(screen.queryByLabelText('Sin leer')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Marcar como leído' })).toBeNull();
  });

  it('shows the amount owed in a balance notice', () => {
    render(
      <NotificationListItem
        notification={notification({ type: 'BalanceDue', balance: { amount: 50000, currency: 'CRC' } })}
        onSendWhatsApp={jest.fn()}
        onOpenBooking={jest.fn()}
        onMarkRead={jest.fn()}
      />,
    );

    expect(screen.getByText('Saldo pendiente')).toBeTruthy();
    expect(screen.getByText(/debe ₡/)).toBeTruthy();
  });
});
