import { MaterialIcons } from '@expo/vector-icons';
import { useEffect, useState, type ReactElement, type ReactNode } from 'react';
import { Animated, Image, Modal, Pressable, StyleSheet, useWindowDimensions, View } from 'react-native';
import { Badge, Divider, Drawer, Text, useTheme } from 'react-native-paper';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBottomInset } from '../hooks/useBottomInset';
import { useMenuCounters } from '../hooks/useMenuCounters';
import { useTopInset } from '../hooks/useTopInset';
import { useMenuStore } from '../menu/menuStore';
import { openNotifications, openRefunds, openSettings } from '../navigation/appNavigation';
import { confirmSignOut } from '../session/confirmSignOut';
import { useSessionStore } from '../session/sessionStore';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import { withAlpha } from '../utils/color';
import { inboxLabel } from '../utils/notifications';
import { refundsLabel } from '../utils/billing';
import type { MenuIconProps } from '../types/components/AppMenu.types';

/** The mark of the logo, already small. */
const LOGO_MARK = require('../../assets/logo-mark.png') as number;

/** Widest the panel gets, in points. */
const PANEL_MAX_WIDTH = 340;

/** The share of the screen the panel takes on a phone. */
const PANEL_SCREEN_SHARE = 0.82;

/** How long the panel takes to slide in, in milliseconds. */
const OPEN_MS = 220;

/** How dark the rest of the screen gets while the menu is open: 0 leaves it as it is, 1 hides it. */
const MENU_DIMMING = 0.7;

/**
 * Makes the function that draws the icon of an item of the menu. Made once, outside the menu, so the menu does not define a new
 * component every time it is drawn.
 * @param name Name of the icon in the font of the icons.
 * @returns The function the menu library calls to draw the icon.
 */
function menuIcon(name: keyof typeof MaterialIcons.glyphMap): (props: MenuIconProps) => ReactElement {
  return function MenuIcon({ size, color }: MenuIconProps): ReactElement {
    return <MaterialIcons name={name} size={size} color={color} />;
  };
}

/** The icons of the items of the menu. */
const ICONS = {
  home: menuIcon('home'),
  notices: menuIcon('notifications'),
  refunds: menuIcon('attach-money'),
  settings: menuIcon('settings'),
  signOut: menuIcon('logout'),
} as const;

/**
 * Makes the function that draws the counter at the right of an item, which draws nothing while there is nothing to count.
 * @param count What is waiting.
 * @param background Color of the counter.
 * @param color Color of its number.
 * @returns The function the menu library calls to draw the right side of the item.
 */
function counter(count: number, background: string, color: string): () => ReactNode {
  return function MenuCounter(): ReactNode {
    return count > 0 ? <Badge style={{ backgroundColor: background, color }}>{count}</Badge> : null;
  };
}

/**
 * The side menu of the app: a panel that slides in from the left over a dimmed screen. At the top, on the band of the brand, the
 * logo, the name of the studio and who is signed in; then Inicio, Avisos and Reembolsos pendientes (with what is waiting), Ajustes
 * and, apart at the end, Cerrar sesión. It closes by choosing something, by tapping the dimmed screen or with the back button.
 * The panel slides in when the menu opens and the whole menu fades out when it closes. It is drawn above the whole app, so the
 * button of the header can be anywhere. Without a signed-in photographer it shows nothing.
 * @returns The menu; it draws nothing while it is closed.
 */
export function AppMenu(): ReactElement | null {
  const styles = useStyles();
  const theme = useTheme();
  const { palette } = useAppTheme();
  const open = useMenuStore((state) => state.open);
  const closeMenu = useMenuStore((state) => state.closeMenu);
  const session = useSessionStore((state) => state.session);
  const { unreadCount, pendingRefunds } = useMenuCounters();
  const topInset = useTopInset();
  const bottomInset = useBottomInset();
  const { width } = useWindowDimensions();
  const panelWidth = Math.min(width * PANEL_SCREEN_SHARE, PANEL_MAX_WIDTH);
  const [progress] = useState(() => new Animated.Value(0));

  // Each time the menu opens, the panel slides in from the left.
  useEffect(() => {
    if (open) {
      progress.setValue(0);
      Animated.timing(progress, { toValue: 1, duration: OPEN_MS, useNativeDriver: true }).start();
    }
  }, [open, progress]);

  if (session === null) {
    return null;
  }

  /**
   * Closes the menu and then does what the photographer chose.
   * @param action What to do once the menu is closing.
   * @returns A handler for the item.
   */
  const choose = (action: () => void) => () => {
    closeMenu();
    action();
  };

  return (
    <Modal visible={open} transparent animationType="fade" statusBarTranslucent onRequestClose={closeMenu}>
      <View style={styles.root}>
        <Animated.View
          style={[StyleSheet.absoluteFill, { backgroundColor: withAlpha(palette.scrim, MENU_DIMMING) }]}
        >
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Cerrar el menú de navegación"
            onPress={closeMenu}
            style={StyleSheet.absoluteFill}
          />
        </Animated.View>
        <Animated.View
          style={[
            styles.panel,
            {
              width: panelWidth,
              backgroundColor: theme.colors.surface,
              transform: [{ translateX: progress.interpolate({ inputRange: [0, 1], outputRange: [-panelWidth, 0] }) }],
            },
          ]}
        >
          <View style={[styles.band, { paddingTop: topInset + spacing.lg }]}>
            <Image source={LOGO_MARK} accessible={false} importantForAccessibility="no" style={styles.logo} />
            <Text style={styles.studio} accessibilityRole="header" maxFontSizeMultiplier={1.2}>
              PhotoSense Studio
            </Text>
            <Text style={styles.name} numberOfLines={1}>
              {session.name}
            </Text>
            {session.email.length > 0 && (
              <Text style={styles.email} numberOfLines={1}>
                {session.email}
              </Text>
            )}
          </View>

          <View style={[styles.items, { paddingBottom: bottomInset + spacing.md }]}>
            <Drawer.Item
              label="Inicio"
              active
              icon={ICONS.home}
              onPress={closeMenu}
            />
            <Drawer.Item
              label="Avisos"
              accessibilityLabel={inboxLabel(unreadCount)}
              icon={ICONS.notices}
              right={counter(unreadCount, palette.primary, palette.onPrimary)}
              onPress={choose(openNotifications)}
            />
            <Drawer.Item
              label="Reembolsos pendientes"
              accessibilityLabel={pendingRefunds > 0 ? refundsLabel(pendingRefunds) : 'Reembolsos pendientes'}
              icon={ICONS.refunds}
              right={counter(pendingRefunds, palette.error, palette.onError)}
              onPress={choose(openRefunds)}
            />
            <Drawer.Item
              label="Ajustes"
              icon={ICONS.settings}
              onPress={choose(openSettings)}
            />
            <Divider style={styles.divider} />
            <Drawer.Item
              label="Cerrar sesión"
              icon={ICONS.signOut}
              onPress={choose(confirmSignOut)}
            />
          </View>
        </Animated.View>
      </View>
    </Modal>
  );
}

/** Styles of the menu: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  root: {
    flex: 1,
  },
  panel: {
    bottom: 0,
    left: 0,
    position: 'absolute',
    top: 0,
  },
  band: {
    backgroundColor: palette.brand,
    gap: spacing.xs,
    padding: spacing.lg,
  },
  logo: {
    borderColor: palette.silver,
    borderRadius: radius.sm,
    borderWidth: 2,
    height: 56,
    marginBottom: spacing.sm,
    width: 56,
  },
  studio: {
    color: palette.onBrand,
    fontFamily: FONT_FAMILIES.title,
    fontSize: 18,
    letterSpacing: 2,
    textTransform: 'uppercase',
  },
  name: {
    color: palette.onBrand,
    fontFamily: FONT_FAMILIES.bodyBold,
    fontSize: 16,
    marginTop: spacing.sm,
  },
  email: {
    color: palette.silver,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 14,
  },
  items: {
    flex: 1,
    padding: spacing.sm,
  },
  divider: {
    marginVertical: spacing.sm,
  },
}));
