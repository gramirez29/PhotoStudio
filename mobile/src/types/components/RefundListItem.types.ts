import type { SettlementResponse } from '../api/billing';

/** Props of the `RefundListItem` component. */
export interface RefundListItemProps {
  /** Settlement with a pending refund. */
  readonly settlement: SettlementResponse;
  /** Called when the photographer taps "Escribir por WhatsApp". */
  readonly onContact: (settlement: SettlementResponse) => void;
  /** Called when the photographer taps "Marcar como devuelto". */
  readonly onComplete: (settlement: SettlementResponse) => void;
  /** Called when the photographer taps "Ver reserva". */
  readonly onOpenBooking: (settlement: SettlementResponse) => void;
}
