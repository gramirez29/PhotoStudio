import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useRunMaintenance } from '../hooks/useRunMaintenance';
import { colors, spacing } from '../theme/tokens';
import { maintenanceErrorMessage } from '../utils/apiErrors';
import { describeMaintenanceResult } from '../utils/maintenance';
import { ActionButton } from './ActionButton';

/**
 * Button that runs the maintenance pass on demand, so a hold that already ended frees its slot without waiting for the
 * daily scheduled run. Shows what the pass did, or why it could not run.
 * @returns The button with its result message.
 */
export function MaintenanceButton(): ReactElement {
  const maintenance = useRunMaintenance();

  let message: string | null = null;
  if (maintenance.isError) {
    message = maintenanceErrorMessage(maintenance.error);
  } else if (maintenance.isSuccess) {
    message = describeMaintenanceResult(maintenance.data);
  }

  return (
    <View style={styles.container}>
      <ActionButton
        label={maintenance.isPending ? 'Actualizando…' : 'Liberar reservas vencidas'}
        onPress={() => maintenance.mutate()}
        disabled={maintenance.isPending}
      />
      <Text style={styles.hint}>Libera el horario de las reservas cuyo apartado ya venció.</Text>
      {message !== null && <Text style={[styles.message, maintenance.isError && styles.error]}>{message}</Text>}
    </View>
  );
}

/** Styles of the button and its messages. */
const styles = StyleSheet.create({
  container: {
    marginBottom: spacing.md,
  },
  hint: {
    color: colors.muted,
    fontSize: 13,
    marginTop: spacing.xs,
  },
  message: {
    color: colors.success,
    fontSize: 14,
    marginTop: spacing.sm,
  },
  error: {
    color: colors.danger,
  },
});
