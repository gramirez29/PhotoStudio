import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import type { ToggleRowProps } from '../types/components/ToggleRow.types';

/** Width of the track of the switch. */
const TRACK_WIDTH = 52;

/** Height of the track of the switch, and so of the touch target of the row. */
const TRACK_HEIGHT = 32;

/** Diameter of the thumb when the switch is on. */
const THUMB_ON = 24;

/** Diameter of the thumb when the switch is off. */
const THUMB_OFF = 16;

/**
 * A row with a label and a switch, in the style of Material 3 and drawn with the colors of the theme, so it looks the same on every
 * phone and in both themes. On, the track is the main color with the thumb on it; off, the track is the soft color of the surface with
 * an outline and a darker thumb. Both states keep at least 3:1 of contrast against what is behind them, which a test checks. The whole
 * row is the button, so it is easy to hit, and a screen reader says it is a switch and whether it is on.
 * @param props Component props.
 * @returns The row.
 */
export function ToggleRow({ label, accessibilityLabel, value, onValueChange }: ToggleRowProps): ReactElement {
  const styles = useStyles();

  return (
    <Pressable
      accessibilityRole="switch"
      accessibilityLabel={accessibilityLabel ?? label}
      accessibilityState={{ checked: value }}
      onPress={() => onValueChange(!value)}
      style={({ pressed }) => [styles.row, pressed && styles.pressed]}
    >
      <Text style={styles.label}>{label}</Text>
      <View
        testID="toggle-track"
        accessible={false}
        importantForAccessibility="no-hide-descendants"
        style={[styles.track, value ? styles.trackOn : styles.trackOff]}
      >
        <View testID="toggle-thumb" style={value ? styles.thumbOn : styles.thumbOff} />
      </View>
    </Pressable>
  );
}

/** Styles of the row: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.md,
    justifyContent: 'space-between',
    minHeight: 48,
    paddingHorizontal: spacing.md,
  },
  pressed: {
    backgroundColor: palette.surfaceVariant,
  },
  label: {
    color: palette.onSurface,
    flex: 1,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 18,
  },
  track: {
    alignItems: 'center',
    borderRadius: radius.pill,
    borderWidth: 2,
    flexDirection: 'row',
    height: TRACK_HEIGHT,
    paddingHorizontal: spacing.xs,
    width: TRACK_WIDTH,
  },
  trackOn: {
    backgroundColor: palette.primary,
    borderColor: palette.primary,
    justifyContent: 'flex-end',
  },
  trackOff: {
    backgroundColor: palette.surfaceVariant,
    borderColor: palette.outline,
    justifyContent: 'flex-start',
  },
  thumbOn: {
    backgroundColor: palette.onPrimary,
    borderRadius: radius.pill,
    height: THUMB_ON,
    width: THUMB_ON,
  },
  thumbOff: {
    backgroundColor: palette.outline,
    borderRadius: radius.pill,
    height: THUMB_OFF,
    width: THUMB_OFF,
  },
}));
