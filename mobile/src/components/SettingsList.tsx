import type { ReactElement } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Text, useTheme } from 'react-native-paper';
import { useBottomInset } from '../hooks/useBottomInset';
import { spacing } from '../theme/tokens';
import { ApiStatus } from './ApiStatus';
import { AppCard } from './AppCard';
import { ChangePasswordButton } from './ChangePasswordButton';
import { ThemeSelector } from './ThemeSelector';

/**
 * Content of the settings screen: what the photographer sets or checks rarely and so does not belong on the home screen. The
 * appearance of the app, the account and whether the server is connected.
 * @returns The sections.
 */
export function SettingsList(): ReactElement {
  const theme = useTheme();
  const bottomInset = useBottomInset();

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
