import type { ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { useNotifications } from '../hooks/useNotifications';
import { openNotifications } from '../navigation/appNavigation';
import { radius, spacing } from '../theme/tokens';
import { inboxLabel } from '../utils/notifications';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Entry to the inbox of notices, with the number of unread ones. The counter comes from the same query the inbox uses, so
 * it stays current while the app is open.
 * @returns The button.
 */
export function NotificationsButton(): ReactElement {
  const styles = useStyles();
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
const useStyles = makeStyles((palette) => ({
  button: {
    alignItems: 'center',
    backgroundColor: palette.surfaceVariant,
    borderRadius: radius.md,
    marginBottom: spacing.md,
    padding: spacing.md,
  },
  highlighted: {
    backgroundColor: palette.primary,
  },
  pressed: {
    opacity: 0.8,
  },
  label: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  highlightedLabel: {
    color: palette.onPrimary,
  },
}));
