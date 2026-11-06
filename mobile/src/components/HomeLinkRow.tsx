import { MaterialIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { HomeLinkRowProps } from '../types/components/home.types';

/** Size of the icons of the row, in points. */
const ICON_SIZE = 24;

/**
 * A row of the home screen that goes somewhere when tapped: an icon, a text, optionally a detail such as a count, and an arrow. The
 * icons are drawings; what a screen reader reads is the label of the row.
 * @param props Component props.
 * @returns The row.
 */
export function HomeLinkRow({ icon, label, detail, accessibilityLabel, onPress }: HomeLinkRowProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel ?? label}
      onPress={onPress}
      style={({ pressed }) => [styles.row, pressed && styles.pressed]}
    >
      <MaterialIcons name={icon} size={ICON_SIZE} color={palette.primary} accessible={false} importantForAccessibility="no" />
      <Text style={styles.label}>{label}</Text>
      {detail !== undefined && <Text style={styles.detail}>{detail}</Text>}
      <MaterialIcons name="chevron-right" size={ICON_SIZE} color={palette.onSurfaceVariant} accessible={false} importantForAccessibility="no" />
    </Pressable>
  );
}

/** Styles of the row: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.md,
    minHeight: 52,
  },
  pressed: {
    opacity: 0.6,
  },
  label: {
    color: palette.onSurface,
    flex: 1,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 16,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.bodyBold,
    fontSize: 14,
  },
}));
