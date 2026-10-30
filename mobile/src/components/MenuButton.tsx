import { MaterialIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Pressable, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { useMenuCounters } from '../hooks/useMenuCounters';
import { useMenuStore } from '../menu/menuStore';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';

/** Size of the icon, in points. */
const ICON_SIZE = 28;

/**
 * Header button that opens the side menu: the three lines. It sits on the band of the brand, so its color is the one for that
 * band. When a notice or a refund is waiting, it carries a dot, so what is urgent is seen without opening the menu. The icon and
 * the dot are drawings: the name of the button, which says so too, is what the screen reader reads.
 * @returns The button.
 */
export function MenuButton(): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const { needsAttention } = useMenuCounters();
  const openMenu = useMenuStore((state) => state.openMenu);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={needsAttention ? 'Abrir el menú, hay avisos o reembolsos pendientes' : 'Abrir el menú'}
      hitSlop={spacing.md}
      onPress={openMenu}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
    >
      <MaterialIcons name="menu" size={ICON_SIZE} color={palette.onBrand} accessible={false} importantForAccessibility="no" />
      {needsAttention && <View testID="menu-attention-dot" style={styles.dot} />}
    </Pressable>
  );
}

/** Styles of the button: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  button: {
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 40,
    minWidth: 40,
  },
  pressed: {
    opacity: 0.6,
  },
  dot: {
    backgroundColor: palette.error,
    borderColor: palette.brand,
    borderRadius: radius.pill,
    borderWidth: 2,
    height: 14,
    position: 'absolute',
    right: 4,
    top: 6,
    width: 14,
  },
}));
