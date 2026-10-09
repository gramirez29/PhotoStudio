import { parseSettlement } from '../../api/billingApi';
import { pendingSettlementPayload } from '../../api/__fixtures__/billingPayloads';
import { buildRefundMessage, describeSettlement, refundWhatsAppUrl, refundsLabel } from '../billing';

const pending = parseSettlement(pendingSettlementPayload);

describe('describeSettlement', () => {
  it('says why, what was paid and what has to go back', () => {
    const lines = describeSettlement(pending);

    expect(lines[0]).toBe('Cancelaste la sesión');
    expect(lines[1]).toContain('Pagado: ₡');
    expect(lines[2]).toContain('Debes devolver ₡');
    expect(lines).toHaveLength(3);
  });

  it('mentions the retained deposit and what goes back beyond it', () => {
    const lines = describeSettlement({
      ...pending,
      reason: 'ClientCancelledLate',
      retained: { amount: 50000, currency: 'CRC' },
      retentionStatus: 'Applied',
      refundAmount: { amount: 30000, currency: 'CRC' },
    });

    expect(lines[0]).toBe('El cliente canceló tarde');
    expect(lines).toContainEqual(expect.stringContaining('Te quedas con ₡'));
    expect(lines).toContainEqual(expect.stringContaining('Debes devolver ₡'));
  });

  it('describes a retention with nothing to give back', () => {
    const lines = describeSettlement({
      ...pending,
      reason: 'ClientAbsent',
      retained: { amount: 50000, currency: 'CRC' },
      retentionStatus: 'Applied',
      refundAmount: { amount: 0, currency: 'CRC' },
      refundStatus: 'None',
    });

    expect(lines.some((line) => line.startsWith('Debes devolver'))).toBe(false);
    expect(lines).toContainEqual(expect.stringContaining('Te quedas con ₡'));
  });

  it('describes a refund that was given back, with method and date', () => {
    const lines = describeSettlement({
      ...pending,
      refundStatus: 'Completed',
      refundMethod: 'SinpeMovil',
      refundCompletedAt: '2026-10-18T10:00:00+00:00',
    });

    expect(lines[2]).toMatch(/^Devuelto ₡.* por SINPE Móvil el .*2026/);
  });

  it('describes a reversed retention and a voided refund', () => {
    const lines = describeSettlement({ ...pending, retentionStatus: 'Reversed', refundStatus: 'Voided' });

    expect(lines).toContain('La retención del anticipo se anuló');
    expect(lines).toContain('El reembolso se anuló');
  });
});

describe('refund contact', () => {
  it('greets the client by first name and gives the amount and package', () => {
    const message = buildRefundMessage(pending);

    expect(message).toMatch(/^Hola María, te escribo para devolverte ₡/);
    expect(message).toContain('Retrato Familiar');
  });

  it('builds the WhatsApp link from the phone of the client, or none without a phone', () => {
    expect(refundWhatsAppUrl(pending)).toMatch(/^https:\/\/wa\.me\/50670189220\?text=Hola%20Mar/);
    expect(refundWhatsAppUrl({ ...pending, clientPhone: '' })).toBeNull();
  });

  it('words the entry on the home screen with the count', () => {
    expect(refundsLabel(2)).toBe('Reembolsos pendientes (2)');
  });
});
