import { useState, type ReactElement } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { EXTRAS } from '../config/pricing';
import { radius, spacing } from '../theme/tokens';
import type { ExtrasModalProps } from '../types/components/ExtrasModal.types';
import type { SelectedExtra } from '../types/utils/pricing.types';
import { extrasTotal, formatColones, quantityOf, withQuantity } from '../utils/pricing';
import { ActionButton } from './ActionButton';
import { ExtraRow } from './ExtraRow';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { withAlpha } from '../utils/color';

/**
 * Modal to choose the extras of a booking: every extra with its unit price and a control to add units, and the total of the
 * extras chosen. What is chosen is only kept when the photographer confirms; closing the modal leaves the booking as it was.
 * Mount it only while it is open, so every opening starts from the extras of the booking.
 * @param props Component props.
 * @returns The modal.
 */
export function ExtrasModal({ selected, onConfirm, onClose }: ExtrasModalProps): ReactElement {
  const styles = useStyles();
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
const useStyles = makeStyles((palette) => ({
  backdrop: {
    backgroundColor: withAlpha(palette.scrim, 0.45),
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  sheet: {
    backgroundColor: palette.background,
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
    color: palette.onSurface,
    fontSize: 18,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  close: {
    color: palette.onSurfaceVariant,
    fontSize: 18,
    fontFamily: FONT_FAMILIES.body,
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
    color: palette.onSurfaceVariant,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  totalValue: {
    color: palette.onSurface,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
}));
