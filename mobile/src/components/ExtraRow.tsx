import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { MAX_EXTRA_QUANTITY } from '../config/pricing';
import { radius, spacing } from '../theme/tokens';
import type { ExtraRowProps } from '../types/components/ExtraRow.types';
import { formatColones } from '../utils/pricing';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Row of the list of extras: its name, the price of one unit and a control to add or remove units.
 * @param props Component props.
 * @returns The row.
 */
export function ExtraRow({ extra, quantity, onChange }: ExtraRowProps): ReactElement {
  const styles = useStyles();
  return (
    <View style={styles.row}>
      <View style={styles.text}>
        <Text style={styles.name}>{extra.label}</Text>
        <Text style={styles.price}>{formatColones(extra.unitPrice)} c/u</Text>
      </View>
      <View style={styles.stepper}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Quitar ${extra.label}`}
          accessibilityState={{ disabled: quantity === 0 }}
          disabled={quantity === 0}
          onPress={() => onChange(quantity - 1)}
          style={[styles.button, quantity === 0 && styles.buttonDisabled]}
        >
          <Text style={styles.buttonText}>−</Text>
        </Pressable>
        <Text accessibilityLabel={`Cantidad de ${extra.label}`} style={styles.quantity}>
          {quantity}
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Agregar ${extra.label}`}
          accessibilityState={{ disabled: quantity >= MAX_EXTRA_QUANTITY }}
          disabled={quantity >= MAX_EXTRA_QUANTITY}
          onPress={() => onChange(quantity + 1)}
          style={[styles.button, quantity >= MAX_EXTRA_QUANTITY && styles.buttonDisabled]}
        >
          <Text style={styles.buttonText}>+</Text>
        </Pressable>
      </View>
    </View>
  );
}

/** Styles of the row. */
const useStyles = makeStyles((palette) => ({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.md,
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  text: {
    flex: 1,
  },
  name: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.body,
  },
  price: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  stepper: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
  },
  button: {
    alignItems: 'center',
    backgroundColor: palette.primary,
    borderRadius: radius.pill,
    height: 36,
    justifyContent: 'center',
    width: 36,
  },
  buttonDisabled: {
    opacity: 0.3,
  },
  buttonText: {
    color: palette.onPrimary,
    fontSize: 20,
    fontFamily: FONT_FAMILIES.title,
  },
  quantity: {
    color: palette.onSurface,
    fontSize: 16,
    minWidth: 28,
    textAlign: 'center',
    fontFamily: FONT_FAMILIES.bodyBold,
  },
}));
