import { MaterialIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Modal, Pressable, Switch, Text, View } from 'react-native';
import { changeAgendaShowAll, changeAgendaView } from '../agenda/agendaManager';
import { useAgendaStore } from '../agenda/agendaStore';
import { AGENDA_VIEWS } from '../constants/agenda';
import { useAppTheme } from '../hooks/useAppTheme';
import { useTopInset } from '../hooks/useTopInset';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import type { AgendaViewMenuProps } from '../types/components/agenda.types';
import { AGENDA_VIEW_LABELS } from '../utils/agenda/labels';
import { effectiveAgendaView, isViewImplemented } from '../utils/agenda/views';
import { withAlpha } from '../utils/color';

/** Height of the bar of the header, so the menu opens right under it. */
const HEADER_HEIGHT = 56;

/** How dark the rest of the screen gets while the menu is open: 0 leaves it as it is, 1 hides it. */
const DIMMING = 0.4;

/** Size of the icons of the menu. */
const ICON_SIZE = 22;

/**
 * The menu of the agenda, which opens from the header at the right: the views to choose from (the one that is open is ticked, and the
 * ones that have no screen yet are dimmed and cannot be chosen) and, apart, the switch that adds to the agenda the bookings that are
 * over. Choosing a view closes the menu; the switch leaves it open so the photographer sees the agenda change behind it.
 * @param props Component props.
 * @returns The menu, which draws nothing while it is closed.
 */
export function AgendaViewMenu({ visible, onClose }: AgendaViewMenuProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const topInset = useTopInset();
  const view = useAgendaStore((state) => effectiveAgendaView(state.view));
  const showAll = useAgendaStore((state) => state.showAll);

  return (
    <Modal visible={visible} transparent animationType="fade" statusBarTranslucent onRequestClose={onClose}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Cerrar el menú de vistas"
        onPress={onClose}
        style={[styles.backdrop, { backgroundColor: withAlpha(palette.scrim, DIMMING), paddingTop: topInset + HEADER_HEIGHT }]}
      >
        {/* A tap on the card must not reach the backdrop behind it, which would close the menu. */}
        <Pressable accessible={false} style={styles.card}>
          <Text accessibilityRole="header" style={styles.heading}>
            Vista
          </Text>
          {AGENDA_VIEWS.map((option) => {
            const available = isViewImplemented(option);
            const selected = option === view;
            return (
              <Pressable
                key={option}
                accessibilityRole="menuitem"
                accessibilityLabel={available ? AGENDA_VIEW_LABELS[option] : `${AGENDA_VIEW_LABELS[option]}, todavía no disponible`}
                accessibilityState={{ selected, disabled: !available }}
                disabled={!available}
                onPress={() => {
                  void changeAgendaView(option);
                  onClose();
                }}
                style={({ pressed }) => [styles.item, pressed && styles.pressed, !available && styles.unavailable]}
              >
                <Text style={[styles.itemLabel, selected && styles.selectedLabel]}>{AGENDA_VIEW_LABELS[option]}</Text>
                {selected && <MaterialIcons name="check" size={ICON_SIZE} color={palette.primary} accessible={false} importantForAccessibility="no" />}
              </Pressable>
            );
          })}
          <View style={styles.divider} />
          <View style={styles.item}>
            <Text style={styles.itemLabel}>Mostrar todas las reservas</Text>
            <Switch
              accessibilityLabel="Mostrar todas las reservas, también las completadas, canceladas y vencidas"
              value={showAll}
              onValueChange={(value) => void changeAgendaShowAll(value)}
              trackColor={{ false: palette.outline, true: palette.primary }}
              thumbColor={palette.surface}
            />
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

/** Styles of the menu: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  backdrop: {
    alignItems: 'flex-end',
    flex: 1,
    paddingHorizontal: spacing.sm,
  },
  card: {
    backgroundColor: palette.surface,
    borderColor: palette.outlineVariant,
    borderRadius: radius.md,
    borderWidth: 1,
    minWidth: 260,
    overflow: 'hidden',
    paddingVertical: spacing.xs,
  },
  heading: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    textTransform: 'uppercase',
  },
  item: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    minHeight: 48,
    paddingHorizontal: spacing.md,
  },
  pressed: {
    backgroundColor: palette.surfaceVariant,
  },
  unavailable: {
    opacity: 0.4,
  },
  itemLabel: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 16,
  },
  selectedLabel: {
    color: palette.primary,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  divider: {
    backgroundColor: palette.outlineVariant,
    height: 1,
    marginVertical: spacing.xs,
  },
}));
