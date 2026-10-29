import type { ExtraOption } from '../config/pricing.types';

/** Props of the `ExtraRow` component. */
export interface ExtraRowProps {
  /** Extra shown in the row. */
  readonly extra: ExtraOption;
  /** Units chosen now; zero when the extra is not chosen. */
  readonly quantity: number;
  /** Called with the new number of units when the photographer taps + or −. */
  readonly onChange: (quantity: number) => void;
}
