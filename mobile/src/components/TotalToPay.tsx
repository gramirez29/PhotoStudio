import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import type { TotalToPayProps } from '../types/components/TotalToPay.types';
import { formatColones } from '../utils/pricing';

/**
 * What the client pays, written bigger than the rest of the screen and in bold.
 * @param props Component props.
 * @returns The total.
 */
export function TotalToPay({ total }: TotalToPayProps): ReactElement {
  return (
    <View style={styles.row}>
      <Text style={styles.label}>Total a pagar</Text>
      <Text accessibilityLabel="Total a pagar" style={styles.value}>
        {formatColones(total)}
      </Text>
    </View>
  );
}

/** Styles of the total. */
const styles = StyleSheet.create({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
  },
  label: {
    color: colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
  },
  value: {
    color: colors.textPrimary,
    fontSize: 26,
    fontWeight: '700',
  },
});
