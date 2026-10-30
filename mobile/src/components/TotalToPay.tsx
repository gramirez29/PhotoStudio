import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { spacing } from '../theme/tokens';
import type { TotalToPayProps } from '../types/components/TotalToPay.types';
import { formatColones } from '../utils/pricing';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * What the client pays, written bigger than the rest of the screen and in bold.
 * @param props Component props.
 * @returns The total.
 */
export function TotalToPay({ total }: TotalToPayProps): ReactElement {
  const styles = useStyles();
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
const useStyles = makeStyles((palette) => ({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
  },
  label: {
    color: palette.onSurface,
    fontSize: 22,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  value: {
    color: palette.onSurface,
    fontSize: 26,
    fontFamily: FONT_FAMILIES.titleBold,
  },
}));
