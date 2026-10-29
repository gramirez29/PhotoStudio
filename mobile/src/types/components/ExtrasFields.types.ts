import type { SelectedExtra } from '../utils/pricing.types';

/** Props of the `ExtrasFields` component. */
export interface ExtrasFieldsProps {
  /** Extras chosen for the booking. */
  readonly extras: readonly SelectedExtra[];
  /** Total of the extras chosen, in colones. */
  readonly extrasTotal: number;
  /** Discount as typed, in colones; empty for none. */
  readonly discount: string;
  /** Validation message of the discount, if any. */
  readonly discountError?: string;
  /** Called with the extras chosen when the photographer confirms the list. */
  readonly onExtrasChange: (extras: readonly SelectedExtra[]) => void;
  /** Called with the discount the photographer types. */
  readonly onDiscountChange: (text: string) => void;
}
