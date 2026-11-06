import type { ReactElement } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Text, useTheme } from 'react-native-paper';
import { useBottomInset } from '../hooks/useBottomInset';
import { useSessionStore } from '../session/sessionStore';
import { spacing } from '../theme/tokens';
import { ApiStatus } from './ApiStatus';
import { AppCard } from './AppCard';
import { ChangePasswordButton } from './ChangePasswordButton';
import { MaintenanceButton } from './MaintenanceButton';
import { ThemeSelector } from './ThemeSelector';

/**
 * Content of the settings screen: what the photographer sets or checks rarely and so does not belong on the home screen. The
 * appearance of the app, the account, whether the server is connected and, for a system operator, the maintenance pass.
 * @returns The sections.
 */
export function SettingsList(): ReactElement {
  const theme = useTheme();
  const bottomInset = useBottomInset();
  // The maintenance pass works on every photographer's data: the backend only accepts it from system operators.
  const isSystemOperator = useSessionStore((state) => state.session?.isSystemOperator === true);

  return (
    <ScrollView contentContainerStyle={[styles.container, { paddingBottom: spacing.md + bottomInset }]}>
      <AppCard style={styles.card}>
        <ThemeSelector />
      </AppCard>
      <AppCard style={styles.card}>
        <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
          Cuenta
        </Text>
        <ChangePasswordButton />
      </AppCard>
      <AppCard style={styles.card}>
        <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
          Servidor
        </Text>
        <ApiStatus />
      </AppCard>
      {isSystemOperator && (
        <AppCard style={styles.card}>
          <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
            Mantenimiento
          </Text>
          <MaintenanceButton />
        </AppCard>
      )}
    </ScrollView>
  );
}

/** Layout of the sections. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    padding: spacing.md,
  },
  card: {
    gap: spacing.sm,
  },
});
