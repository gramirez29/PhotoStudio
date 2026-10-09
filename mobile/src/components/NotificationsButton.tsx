import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { useNotifications } from '../hooks/useNotifications';
import { openNotifications } from '../navigation/appNavigation';
import { colors, radius, spacing } from '../theme/tokens';
import { inboxLabel } from '../utils/notifications';

/**
 * Entry to the inbox of notices, with the number of unread ones. The counter comes from the same query the inbox uses, so
 * it stays current while the app is open.
 * @returns The button.
 */
export function NotificationsButton(): ReactElement {
  const unreadCount = useNotifications().data?.unreadCount ?? 0;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={inboxLabel(unreadCount)}
      onPress={openNotifications}
      style={({ pressed }) => [styles.button, unreadCount > 0 && styles.highlighted, pressed && styles.pressed]}
    >
      <Text style={[styles.label, unreadCount > 0 && styles.highlightedLabel]}>{inboxLabel(unreadCount)}</Text>
    </Pressable>
  );
}

/** Styles of the button. */
const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    marginBottom: spacing.md,
    padding: spacing.md,
  },
  highlighted: {
    backgroundColor: colors.primary,
  },
  pressed: {
    opacity: 0.8,
  },
  label: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '700',
  },
  highlightedLabel: {
    color: colors.textInverse,
  },
});
