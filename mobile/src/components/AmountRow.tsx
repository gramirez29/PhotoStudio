import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import type { MoneyResponse } from '../api/types';
import { colors, spacing } from '../theme/tokens';
import { formatMoney } from '../utils/format';

/** Props of {@link AmountRow}. */
export interface AmountRowProps {
  /** Label shown on the left. */
  readonly label: string;
  /** Amount shown on the right. */
  readonly money: MoneyResponse;
  /** Whether to emphasize the row (for example, the balance). */
  readonly emphasized?: boolean;
}

/**
 * Row with a label and a formatted amount.
 * @param props Component props.
 * @returns The row.
 */
export function AmountRow({ label, money, emphasized = false }: AmountRowProps): ReactElement {
  return (
    <View style={styles.row}>
      <Text style={[styles.label, emphasized && styles.emphasized]}>{label}</Text>
      <Text style={[styles.value, emphasized && styles.emphasized]}>{formatMoney(money)}</Text>
    </View>
  );
}

/** Styles of the row. */
const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.xs,
  },
  label: {
    color: colors.textSecondary,
    fontSize: 15,
  },
  value: {
    color: colors.textPrimary,
    fontSize: 15,
  },
  emphasized: {
    color: colors.textPrimary,
    fontWeight: '700',
  },
});
