import { MaterialIcons } from '@expo/vector-icons';
import { useState, type ReactElement } from 'react';
import { Pressable, View } from 'react-native';
import { useAgendaStore } from '../agenda/agendaStore';
import { useAppTheme } from '../hooks/useAppTheme';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import { AgendaViewMenu } from './AgendaViewMenu';

/** Size of the icons, in points. */
const ICON_SIZE = 26;

/**
 * The buttons at the right of the header of the agenda: "today", which brings the agenda back to the present, and the one that opens
 * the menu of views. They sit on the band of the brand, so their color is the one for that band; the icons are drawings and the name of
 * each button is what the screen reader reads.
 * @returns The buttons and the menu they open.
 */
export function AgendaHeaderActions(): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const goToToday = useAgendaStore((state) => state.goToToday);
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <View style={styles.row}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Ir a hoy"
        hitSlop={spacing.sm}
        onPress={goToToday}
        style={({ pressed }) => [styles.button, pressed && styles.pressed]}
      >
        <MaterialIcons name="today" size={ICON_SIZE} color={palette.onBrand} accessible={false} importantForAccessibility="no" />
      </Pressable>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Elegir la vista de la agenda"
        accessibilityState={{ expanded: menuOpen }}
        hitSlop={spacing.sm}
        onPress={() => setMenuOpen(true)}
        style={({ pressed }) => [styles.button, pressed && styles.pressed]}
      >
        <MaterialIcons name="more-vert" size={ICON_SIZE} color={palette.onBrand} accessible={false} importantForAccessibility="no" />
      </Pressable>
      <AgendaViewMenu visible={menuOpen} onClose={() => setMenuOpen(false)} />
    </View>
  );
}

/** Styles of the buttons. */
const useStyles = makeStyles(() => ({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.xs,
  },
  button: {
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 40,
    minWidth: 40,
  },
  pressed: {
    opacity: 0.6,
  },
}));
