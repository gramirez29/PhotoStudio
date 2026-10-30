import type { ReactElement } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Text, useTheme } from 'react-native-paper';
import { useBottomInset } from '../hooks/useBottomInset';
import { spacing } from '../theme/tokens';
import { AppCard } from './AppCard';
import { ChangePasswordButton } from './ChangePasswordButton';
import { ThemeSelector } from './ThemeSelector';

/**
 * Content of the settings screen: what the photographer sets rarely and so does not belong on the home screen. The appearance
 * of the app, and the account.
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
