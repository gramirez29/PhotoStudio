import { useState, type ReactElement } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { EXTRAS } from '../config/pricing';
import { colors, radius, spacing } from '../theme/tokens';
import type { ExtrasModalProps } from '../types/components/ExtrasModal.types';
import type { SelectedExtra } from '../types/utils/pricing.types';
import { extrasTotal, formatColones, quantityOf, withQuantity } from '../utils/pricing';
import { ActionButton } from './ActionButton';
import { ExtraRow } from './ExtraRow';

/**
 * Modal to choose the extras of a booking: every extra with its unit price and a control to add units, and the total of the
 * extras chosen. What is chosen is only kept when the photographer confirms; closing the modal leaves the booking as it was.
 * Mount it only while it is open, so every opening starts from the extras of the booking.
 * @param props Component props.
 * @returns The modal.
 */
export function ExtrasModal({ selected, onConfirm, onClose }: ExtrasModalProps): ReactElement {
  const [draft, setDraft] = useState<readonly SelectedExtra[]>(selected);

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.sheet}>
          <View style={styles.header}>
            <Text style={styles.title}>Extras</Text>
            <Pressable accessibilityRole="button" accessibilityLabel="Cerrar extras" onPress={onClose} hitSlop={spacing.sm}>
              <Text style={styles.close}>✕</Text>
            </Pressable>
          </View>
          <ScrollView>
            {EXTRAS.map((extra) => (
              <ExtraRow
                key={extra.code}
                extra={extra}
                quantity={quantityOf(draft, extra.code)}
                onChange={(quantity) => setDraft((previous) => withQuantity(previous, extra, quantity))}
              />
            ))}
          </ScrollView>
          <View style={styles.footer}>
            <View style={styles.totalRow}>
              <Text style={styles.totalLabel}>Total en extras</Text>
              <Text style={styles.totalValue}>{formatColones(extrasTotal(draft))}</Text>
            </View>
            <ActionButton label="Agregar Extras" onPress={() => onConfirm(draft)} />
          </View>
        </View>
      </View>
    </Modal>
  );
}

/** Styles of the modal. */
const styles = StyleSheet.create({
  backdrop: {
    backgroundColor: colors.overlay,
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  sheet: {
    backgroundColor: colors.background,
    borderRadius: radius.md,
    maxHeight: '85%',
    paddingVertical: spacing.sm,
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 18,
    fontWeight: '700',
  },
  close: {
    color: colors.textSecondary,
    fontSize: 18,
  },
  footer: {
    gap: spacing.sm,
    padding: spacing.md,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  totalLabel: {
    color: colors.textSecondary,
    fontSize: 15,
  },
  totalValue: {
    color: colors.textPrimary,
    fontSize: 15,
    fontWeight: '700',
  },
});
