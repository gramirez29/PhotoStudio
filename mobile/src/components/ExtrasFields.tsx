import { useState, type ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { ExtrasFieldsProps } from '../types/components/ExtrasFields.types';
import { formatColones } from '../utils/pricing';
import { ExtrasModal } from './ExtrasModal';
import { FormInput } from './FormInput';

/**
 * The extras of a booking and its discount: a button that opens the list of extras, a read-only field with their total, and the
 * optional discount.
 * @param props Component props.
 * @returns The fields and, while it is open, the list of extras.
 */
export function ExtrasFields({ extras, extrasTotal, discount, discountError, onExtrasChange, onDiscountChange }: ExtrasFieldsProps): ReactElement {
  const [open, setOpen] = useState(false);

  return (
    <View style={styles.container}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Agregar Extras"
        onPress={() => setOpen(true)}
        style={({ pressed }) => [styles.button, pressed && styles.pressed]}
      >
        <Text style={styles.buttonLabel}>Agregar Extras</Text>
      </Pressable>
      <FormInput
        label="Extras (₡)"
        value={extras.length === 0 ? '' : formatColones(extrasTotal)}
        editable={false}
        placeholder="Sin extras"
      />
      <FormInput
        label="Descuento (₡), opcional"
        value={discount}
        onChangeText={onDiscountChange}
        error={discountError}
        keyboardType="number-pad"
        placeholder="0"
      />
      {open && (
        <ExtrasModal
          selected={extras}
          onConfirm={(chosen) => {
            onExtrasChange(chosen);
            setOpen(false);
          }}
          onClose={() => setOpen(false)}
        />
      )}
    </View>
  );
}

/** Styles of the fields. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
  },
  button: {
    alignItems: 'center',
    borderColor: colors.primary,
    borderRadius: radius.sm,
    borderWidth: 1,
    paddingVertical: spacing.md,
  },
  pressed: {
    opacity: 0.8,
  },
  buttonLabel: {
    color: colors.primary,
    fontSize: 16,
    fontWeight: '600',
  },
});
