import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, StyleSheet, Text } from 'react-native';
import { ListSeparator } from '../components/ListSeparator';
import { Placeholder } from '../components/Placeholder';
import { RefundListItem } from '../components/RefundListItem';
import { useRefundActions } from '../hooks/useRefundActions';
import { useRefunds } from '../hooks/useRefunds';
import { colors, spacing } from '../theme/tokens';

/**
 * Pending refunds: money that belongs to clients whose booking ended (cancelled, expired or absent) and that the
 * photographer has not given back yet.
 * @returns The screen.
 */
export default function RefundsScreen(): ReactElement {
  const query = useRefunds();
  const actions = useRefundActions();
  const items = query.data?.items ?? [];

  let empty: ReactElement;
  if (query.isPending) {
    empty = <ActivityIndicator color={colors.primary} accessibilityLabel="Cargando reembolsos" style={styles.loader} />;
  } else if (query.isError) {
    empty = (
      <Placeholder
        message="No se pudieron cargar los reembolsos. Revisa la conexión e inténtalo de nuevo."
        onRetry={() => void query.refetch()}
      />
    );
  } else {
    empty = <Placeholder message="No tienes reembolsos pendientes." />;
  }

  return (
    <FlatList
      data={items}
      keyExtractor={(settlement) => settlement.id}
      renderItem={({ item }) => (
        <RefundListItem
          settlement={item}
          onContact={actions.contact}
          onComplete={actions.complete}
          onOpenBooking={actions.openBooking}
        />
      )}
      ItemSeparatorComponent={ListSeparator}
      ListHeaderComponent={actions.error !== null ? <Text style={styles.error}>{actions.error}</Text> : null}
      ListEmptyComponent={empty}
      refreshing={query.isRefetching}
      onRefresh={() => void query.refetch()}
      contentContainerStyle={styles.container}
    />
  );
}

/** Styles of the screen. */
const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
  },
  error: {
    color: colors.danger,
    fontSize: 14,
    marginBottom: spacing.md,
  },
  loader: {
    marginTop: spacing.lg,
  },
});
