import type { ReactElement } from 'react';
import type { AmountRowProps } from '../types/components/AmountRow.types';
import { formatMoney } from '../utils/format';

/**
 * Row with a label and a formatted amount, for a list of amounts (a description list).
 * @param props Component props.
 * @returns The row.
 */
export function AmountRow({ label, money, emphasized = false }: AmountRowProps): ReactElement {
  return (
    <div className={emphasized ? 'amount-row amount-row--emphasized' : 'amount-row'}>
      <dt>{label}</dt>
      <dd>{formatMoney(money)}</dd>
    </div>
  );
}
