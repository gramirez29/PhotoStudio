import type { ReactElement } from 'react';
import type { BookingViewProps } from '../types/components/BookingView.types';
import { nextSteps } from '../utils/booking';
import { formatDateTime, formatTimeRange } from '../utils/format';
import { BOOKING_STATUS_LABELS, BOOKING_STATUS_TONES } from '../utils/labels';
import { AmountRow } from './AmountRow';
import { PaymentsList } from './PaymentsList';
import { StatusBadge } from './StatusBadge';

/**
 * What the client sees when the link opens: whose booking it is, when, how much, what is paid and what comes next. It only
 * shows what the API sent; it never decides by itself what a status allows.
 * @param props Component props.
 * @returns The booking.
 */
export function BookingView({ booking, action }: BookingViewProps): ReactElement {
  const steps = nextSteps(booking);

  return (
    <div className="stack">
      <StatusBadge label={BOOKING_STATUS_LABELS[booking.status]} tone={BOOKING_STATUS_TONES[booking.status]} />

      <header className="stack stack--tight">
        <h1>Hola, {booking.clientName}</h1>
        <p className="muted">
          Tu sesión de {booking.packageName}
          {booking.photographerName.length > 0 && <> con {booking.photographerName}</>}
        </p>
      </header>

      <section className="card" aria-labelledby="session-title">
        <h2 id="session-title">Sesión</h2>
        <p>{formatDateTime(booking.sessionStart)}</p>
        <p className="muted">{formatTimeRange(booking.sessionStart, booking.sessionEnd)}</p>
      </section>

      {steps.length > 0 && (
        <section className="stack notice" aria-label="Qué sigue">
          {steps.map((step) => (
            <p key={step}>{step}</p>
          ))}
        </section>
      )}

      {action}

      <section className="card" aria-labelledby="amounts-title">
        <h2 id="amounts-title">Montos</h2>
        <dl className="amounts">
          <AmountRow label="Precio del paquete" money={booking.packagePrice} />
          <AmountRow label="Anticipo requerido" money={booking.depositRequired} />
          <AmountRow label="Pagado" money={booking.totalPaid} />
          <AmountRow label="Saldo" money={booking.balance} emphasized />
        </dl>
      </section>

      <PaymentsList payments={booking.payments} />

      <section className="card" aria-labelledby="contract-title">
        <h2 id="contract-title">Contrato</h2>
        <p>{booking.contractSigned ? 'Firmado' : 'Pendiente de firma'}</p>
      </section>
    </div>
  );
}
