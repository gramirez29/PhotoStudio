import type { SelectedExtra } from '../utils/pricing.types';

/** Props of the `ExtrasModal` component. */
export interface ExtrasModalProps {
  /** Extras already chosen for the booking; the list starts from them. */
  readonly selected: readonly SelectedExtra[];
  /** Called with the extras chosen when the photographer confirms. */
  readonly onConfirm: (extras: readonly SelectedExtra[]) => void;
  /** Called when the photographer closes the list without confirming. */
  readonly onClose: () => void;
}
