import type { ReactElement } from 'react';
import { ActivityIndicator, Pressable, Text } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { ScheduleEdgeButtonProps } from '../types/components/agenda.types';

/**
 * The button at an end of the list view that loads more days, for when the list is too short to scroll. While it loads, it shows a
 * spinner in its place.
 * @param props Component props.
 * @returns The button.
 */
export function ScheduleEdgeButton({ label, onPress, loading }: ScheduleEdgeButtonProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ busy: loading }}
      disabled={loading}
      onPress={onPress}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      {loading ? <ActivityIndicator color={palette.primary} /> : <Text style={styles.label}>{label}</Text>}
    </Pressable>
  );
}

/** Styles of the button: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  button: {
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 44,
    paddingVertical: spacing.sm,
  },
  pressed: {
    opacity: 0.6,
  },
  label: {
    color: palette.primary,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 14,
    textDecorationLine: 'underline',
  },
}));
