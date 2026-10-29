import type { ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { useRefunds } from '../hooks/useRefunds';
import { openRefunds } from '../navigation/appNavigation';
import { radius, spacing } from '../theme/tokens';
import { refundsLabel } from '../utils/billing';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Entry to the refunds the photographer still owes. It is shown only while there is at least one, so a photographer with
 * nothing to give back sees no extra button.
 * @returns The button, or nothing when no refund is pending.
 */
export function RefundsButton(): ReactElement | null {
  const styles = useStyles();
  const pendingCount = useRefunds().data?.pendingCount ?? 0;

  if (pendingCount === 0) {
    return null;
  }

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={refundsLabel(pendingCount)}
      onPress={openRefunds}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      <Text style={styles.label}>{refundsLabel(pendingCount)}</Text>
    </Pressable>
  );
}

/** Styles of the button. */
const useStyles = makeStyles((palette) => ({
  button: {
    alignItems: 'center',
    backgroundColor: palette.error,
    borderRadius: radius.md,
    marginBottom: spacing.md,
    padding: spacing.md,
  },
  pressed: {
    opacity: 0.8,
  },
  label: {
    color: palette.onPrimary,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
}));
