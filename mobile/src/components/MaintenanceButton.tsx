import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { useRunMaintenance } from '../hooks/useRunMaintenance';
import { spacing } from '../theme/tokens';
import { maintenanceErrorMessage } from '../utils/apiErrors';
import { describeMaintenanceResult } from '../utils/maintenance';
import { ActionButton } from './ActionButton';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Button that runs the maintenance pass on demand, so a hold that already ended frees its slot without waiting for the
 * daily scheduled run. Shows what the pass did, or why it could not run.
 * @returns The button with its result message.
 */
export function MaintenanceButton(): ReactElement {
  const styles = useStyles();
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
const useStyles = makeStyles((palette) => ({
  container: {
    marginBottom: spacing.md,
  },
  hint: {
    color: palette.muted,
    fontSize: 13,
    marginTop: spacing.xs,
    fontFamily: FONT_FAMILIES.body,
  },
  message: {
    color: palette.success,
    fontSize: 14,
    marginTop: spacing.sm,
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.error,
  },
}));
