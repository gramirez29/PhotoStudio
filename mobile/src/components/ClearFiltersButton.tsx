import { MaterialCommunityIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Pressable } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { makeStyles } from '../theme/makeStyles';
import { radius } from '../theme/tokens';
import type { ClearFiltersButtonProps } from '../types/components/ClearFiltersButton.types';

/** Size of the icon, in points. */
const ICON_SIZE = 22;

/**
 * Round button with a broom that sweeps away every filter at once. The icon is a drawing: the name of the button is what the
 * screen reader reads.
 * @param props Component props.
 * @returns The button.
 */
export function ClearFiltersButton({ onPress }: ClearFiltersButtonProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Limpiar filtros"
      hitSlop={6}
      onPress={onPress}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      <MaterialCommunityIcons name="broom" size={ICON_SIZE} color={palette.onSurface} accessible={false} importantForAccessibility="no" />
    </Pressable>
  );
}

/** Styles of the button: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  button: {
    alignItems: 'center',
    borderColor: palette.outlineVariant,
    borderRadius: radius.pill,
    borderWidth: 1,
    height: 40,
    justifyContent: 'center',
    width: 40,
  },
  pressed: {
    opacity: 0.6,
  },
}));
