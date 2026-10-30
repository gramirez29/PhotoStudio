import type { ReactElement } from 'react';
import { ActivityIndicator, FlatList, Text, View } from 'react-native';
import { ActionButton } from '../components/ActionButton';
import { ListSeparator } from '../components/ListSeparator';
import { LoadMoreFooter } from '../components/LoadMoreFooter';
import { NotificationListItem } from '../components/NotificationListItem';
import { Placeholder } from '../components/Placeholder';
import { useNotificationActions } from '../hooks/useNotificationActions';
import { useNotifications } from '../hooks/useNotifications';
import { flattenPages } from '../utils/pagination';
import { spacing } from '../theme/tokens';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBottomInset } from '../hooks/useBottomInset';

/**
 * Inbox of notices: session reminders and balances still owed, with the actions to message the client on WhatsApp.
 * @returns The screen.
 */
export default function NotificationsScreen(): ReactElement {
  const bottomInset = useBottomInset();
  const styles = useStyles();
  const { palette } = useAppTheme();
  const query = useNotifications();
  const actions = useNotificationActions();
  const items = flattenPages(query.data?.pages);
  const unreadCount = query.data?.pages[0]?.unreadCount ?? 0;

  let empty: ReactElement;
  if (query.isPending) {
    empty = <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando avisos" style={styles.loader} />;
  } else if (query.isError) {
    empty = (
      <Placeholder
        message="No se pudieron cargar los avisos. Revisa la conexión e inténtalo de nuevo."
        onRetry={() => void query.refetch()}
      />
    );
  } else {
    empty = <Placeholder message="No tienes avisos. Aquí verás los recordatorios de sesiones y los saldos pendientes." />;
  }

  return (
    <FlatList
      data={items}
      keyExtractor={(notification) => notification.id}
      renderItem={({ item }) => (
        <NotificationListItem
          notification={item}
          onSendWhatsApp={actions.sendWhatsApp}
          onOpenBooking={actions.openBooking}
          onMarkRead={actions.markRead}
        />
      )}
      ItemSeparatorComponent={ListSeparator}
      ListHeaderComponent={
        <View style={styles.header}>
          {actions.error !== null && <Text style={styles.error}>{actions.error}</Text>}
          {unreadCount > 0 && (
            <ActionButton
              label={actions.isMarkingAll ? 'Marcando…' : 'Marcar todo como leído'}
              onPress={actions.markAllRead}
              disabled={actions.isMarkingAll}
            />
          )}
        </View>
      }
      ListEmptyComponent={empty}
      ListFooterComponent={
        <LoadMoreFooter
          hasMore={query.hasNextPage}
          isLoading={query.isFetchingNextPage}
          failed={query.isFetchNextPageError}
          onLoadMore={() => void query.fetchNextPage()}
        />
      }
      refreshing={query.isRefetching && !query.isFetchingNextPage}
      onRefresh={() => void query.refetch()}
      contentContainerStyle={[styles.container, { paddingBottom: spacing.md + bottomInset }]}
    />
  );
}

/** Styles of the screen. */
const useStyles = makeStyles((palette) => ({
  container: {
    padding: spacing.md,
  },
  header: {
    gap: spacing.sm,
    marginBottom: spacing.md,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  loader: {
    marginTop: spacing.lg,
  },
}));
