import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { Text, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { AmountRowProps } from '../types/components/AmountRow.types';
import { formatMoney } from '../utils/format';

/**
 * Row with a label and a formatted amount.
 * @param props Component props.
 * @returns The row.
 */
export function AmountRow({ label, money, emphasized = false }: AmountRowProps): ReactElement {
  const theme = useTheme();
  const variant = emphasized ? 'titleSmall' : 'bodyLarge';
  const color = emphasized ? theme.colors.onSurface : theme.colors.onSurfaceVariant;

  return (
    <View style={styles.row}>
      <Text variant={variant} style={{ color }}>
        {label}
      </Text>
      <Text variant={variant} style={{ color: theme.colors.onSurface }}>
        {formatMoney(money)}
      </Text>
    </View>
  );
}

/** Layout of the row. */
const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.xs,
  },
});
