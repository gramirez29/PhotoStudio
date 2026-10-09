/** A pending-refund settlement payload as the backend produces it (the photographer cancelled a booking with 50 000 paid). */
export const pendingSettlementPayload = {
  id: '0197a000-0000-7000-8000-0000000000a1',
  bookingId: '0197a000-0000-7000-8000-0000000000b1',
  clientName: 'María Pérez Solano',
  clientPhone: '+50670189220',
  packageName: 'Retrato Familiar',
  sessionStart: '2026-10-17T21:00:00+00:00',
  reason: 'PhotographerCancelled',
  totalPaid: { amount: 50000, currency: 'CRC' },
  retained: { amount: 0, currency: 'CRC' },
  retentionStatus: 'None',
  refundAmount: { amount: 50000, currency: 'CRC' },
  refundStatus: 'Pending',
  refundMethod: null,
  refundNote: null,
  refundCompletedAt: null,
  createdAt: '2026-10-16T21:00:00+00:00',
} as const;
