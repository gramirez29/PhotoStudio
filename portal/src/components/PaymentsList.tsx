import type { ReactElement } from 'react';
import type { PaymentsListProps } from '../types/components/PaymentsList.types';
import { formatDay, formatMoney } from '../utils/format';
import { PAYMENT_STATUS_LABELS, PAYMENT_STATUS_TONES } from '../utils/labels';
import { StatusBadge } from './StatusBadge';

/**
 * List of the payments recorded for the booking, with the day and the state of each one. A proof the photographer has not
 * checked yet shows as "En revisión".
 * @param props Component props.
 * @returns The list, or nothing when there are no payments.
 */
export function PaymentsList({ payments }: PaymentsListProps): ReactElement | null {
  if (payments.length === 0) {
    return null;
  }

  return (
    <section className="card" aria-labelledby="payments-title">
      <h2 id="payments-title">Pagos</h2>
      <ul className="payments">
        {payments.map((payment, index) => (
          <li key={`${payment.recordedAt}-${index}`} className="payment">
            <span>
              {formatMoney(payment.amount)} <span className="muted">· {formatDay(payment.recordedAt)}</span>
            </span>
            <StatusBadge label={PAYMENT_STATUS_LABELS[payment.status]} tone={PAYMENT_STATUS_TONES[payment.status]} />
          </li>
        ))}
      </ul>
    </section>
  );
}
