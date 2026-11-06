import { useMemo, useState, type ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { ActivityIndicator, Button } from 'react-native-paper';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBookingStats } from '../hooks/useBookingStats';
import { useToday } from '../hooks/useToday';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { HomePeriodKey } from '../types/hooks/useBookingStats.types';
import { donutSegments } from '../utils/home/donut';
import { periodRange } from '../utils/home/periods';
import { BOOKING_STATUS_LABELS } from '../utils/labels';
import { AppCard } from './AppCard';
import { HomePeriodSelector } from './HomePeriodSelector';
import { StatusDonut } from './StatusDonut';
import { StatusLegend } from './StatusLegend';

/**
 * The card of the home screen with the bookings counted by status: the period selector, the donut chart with the total in the middle
 * and the legend. While the counts load it shows a spinner, and if they cannot be loaded it says so and offers to try again.
 * @returns The card.
 */
export function BookingStatsCard(): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const today = useToday();
  const [period, setPeriod] = useState<HomePeriodKey>('thisMonth');
  const range = useMemo(() => periodRange(period, today), [period, today]);
  const query = useBookingStats(range);
  const stats = query.data;

  let content: ReactElement;
  if (stats !== undefined) {
    const description =
      stats.total === 0
        ? 'Sin citas en este periodo'
        : `${stats.total} ${stats.total === 1 ? 'cita' : 'citas'}: ${stats.byStatus
            .filter((entry) => entry.count > 0)
            .map((entry) => `${entry.count} ${BOOKING_STATUS_LABELS[entry.status].toLowerCase()}`)
            .join(', ')}`;
    content = (
      <>
        <StatusDonut segments={donutSegments(stats)} total={stats.total} accessibilityLabel={description} />
        {stats.total === 0 ? <Text style={styles.empty}>No hay citas en este periodo.</Text> : <StatusLegend stats={stats} />}
      </>
    );
  } else if (query.isError) {
    content = (
      <View style={styles.centered}>
        <Text style={styles.empty}>No se pudieron cargar las citas. Revisa la conexión.</Text>
        <Button mode="outlined" onPress={() => void query.refetch()}>
          Reintentar
        </Button>
      </View>
    );
  } else {
    content = (
      <View style={styles.centered}>
        <ActivityIndicator accessibilityLabel="Cargando las citas" color={palette.primary} />
      </View>
    );
  }

  return (
    <AppCard style={layout.card}>
      <Text accessibilityRole="header" style={styles.title}>
        Tus citas
      </Text>
      <HomePeriodSelector value={period} onChange={setPeriod} />
      {content}
    </AppCard>
  );
}

/** Layout of the card. */
const layout = StyleSheet.create({
  card: {
    gap: spacing.md,
  },
});

/** Styles of the card: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 20,
  },
  centered: {
    alignItems: 'center',
    gap: spacing.sm,
    minHeight: 120,
    justifyContent: 'center',
  },
  empty: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 15,
    textAlign: 'center',
  },
}));
