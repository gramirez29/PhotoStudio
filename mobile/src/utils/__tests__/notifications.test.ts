import type { NotificationResponse } from '../../types/api/notifications';
import {
  buildWhatsAppMessage,
  buildWhatsAppUrl,
  describeNotification,
  firstName,
  inboxLabel,
  whatsAppUrlFor,
} from '../notifications';

/**
 * Builds a session reminder, with the values of the tests unless overridden.
 * @param overrides Values to replace.
 * @returns The notification.
 */
function notification(overrides: Partial<NotificationResponse> = {}): NotificationResponse {
  return {
    id: 'n1',
    type: 'SessionReminder',
    bookingId: 'b1',
    clientName: 'María Pérez Solano',
    clientPhone: '+50670189220',
    packageName: 'Retrato Familiar',
    sessionStart: '2026-10-17T21:00:00+00:00',
    balance: null,
    proofAmount: null,
    selectedPhotos: null,
    extraPhotos: null,
    deliveredAt: '2026-10-16T21:00:00+00:00',
    readAt: null,
    ...overrides,
  };
}

/** A balance notice for 50 000 colones. */
const balanceDue = notification({ type: 'BalanceDue', balance: { amount: 50000, currency: 'CRC' } });

/** A proof-of-payment notice for 25 000 colones. */
const paymentProof = notification({ type: 'PaymentProofSubmitted', proofAmount: { amount: 25000, currency: 'CRC' } });

/** A selection notice: the client chose 12 photos, 2 of them extra. */
const selection = notification({ type: 'SelectionSubmitted', selectedPhotos: 12, extraPhotos: 2 });

describe('the notice of a selection', () => {
  it('says how many photos the client chose and how many are extra', () => {
    expect(describeNotification(selection)).toEqual({
      title: 'Selección recibida',
      detail: 'María Pérez Solano eligió 12 fotos (2 extra) (Retrato Familiar).',
    });
    expect(describeNotification(notification({ type: 'SelectionSubmitted', selectedPhotos: 1, extraPhotos: 0 })).detail).toContain('eligió 1 foto (Retrato');
  });

  it('writes the WhatsApp message that thanks the client and says the editing starts', () => {
    const message = buildWhatsAppMessage(selection);
    expect(message).toContain('Hola María');
    expect(message).toContain('recibí tu selección de 12 fotos (2 extra)');
    expect(message).toContain('Ya empiezo con la edición');
  });
});

describe('describeNotification', () => {
  it('words a session reminder with the client, package and date', () => {
    const text = describeNotification(notification());

    expect(text.title).toBe('Sesión próxima');
    expect(text.detail).toContain('María Pérez Solano');
    expect(text.detail).toContain('Retrato Familiar');
    expect(text.detail).toContain('2026');
  });

  it('words a balance notice with the amount owed', () => {
    const text = describeNotification(balanceDue);

    expect(text.title).toBe('Saldo pendiente');
    expect(text.detail).toContain('₡');
    expect(text.detail.replace(/\D/g, '')).toContain('5000000');
  });
});

describe('describeNotification (proof)', () => {
  it('words a proof notice with the client, the declared amount and the package', () => {
    const text = describeNotification(paymentProof);

    expect(text.title).toBe('Comprobante por verificar');
    expect(text.detail).toContain('María Pérez Solano');
    expect(text.detail).toContain('Retrato Familiar');
    expect(text.detail.replace(/\D/g, '')).toContain('2500000');
  });
});

describe('firstName', () => {
  it('keeps the first word of the name', () => {
    expect(firstName('María Pérez Solano')).toBe('María');
    expect(firstName('  Ana  ')).toBe('Ana');
  });
});

describe('buildWhatsAppMessage', () => {
  it('greets the client by first name and gives the session time in a reminder', () => {
    const message = buildWhatsAppMessage(notification());

    expect(message).toMatch(/^Hola María, te recuerdo tu sesión de Retrato Familiar el /);
    expect(message).toContain('2026');
    expect(message).toMatch(/¡Nos vemos!$/);
  });

  it('asks for the balance in a balance notice', () => {
    const message = buildWhatsAppMessage(balanceDue);

    expect(message).toContain('Hola María, te escribo por el saldo pendiente de ');
    expect(message).toContain('fotos finales');
  });
});

describe('buildWhatsAppMessage (proof)', () => {
  it('acknowledges the proof instead of reminding about a session', () => {
    const message = buildWhatsAppMessage(paymentProof);

    expect(message).toContain('Hola María, recibí tu comprobante de ');
    expect(message).not.toContain('te recuerdo');
  });
});

describe('buildWhatsAppUrl', () => {
  it('keeps only the digits of the phone and encodes the message', () => {
    expect(buildWhatsAppUrl('+506 7018-9220', 'Hola, ¿qué tal?')).toBe(
      `https://wa.me/50670189220?text=${encodeURIComponent('Hola, ¿qué tal?')}`,
    );
  });

  it('returns null when the phone has no digits', () => {
    expect(buildWhatsAppUrl('', 'Hola')).toBeNull();
    expect(buildWhatsAppUrl('sin teléfono', 'Hola')).toBeNull();
  });
});

describe('whatsAppUrlFor', () => {
  it('builds the link of a notification from its phone and message', () => {
    const url = whatsAppUrlFor(notification());

    expect(url).toMatch(/^https:\/\/wa\.me\/50670189220\?text=Hola%20Mar%C3%ADa/);
  });

  it('returns null when the client has no usable phone', () => {
    expect(whatsAppUrlFor(notification({ clientPhone: '' }))).toBeNull();
  });
});

describe('inboxLabel', () => {
  it('shows the unread counter only when there are unread notices', () => {
    expect(inboxLabel(0)).toBe('Avisos');
    expect(inboxLabel(3)).toBe('Avisos (3)');
  });
});
