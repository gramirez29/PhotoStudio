import { useState, type ReactElement } from 'react';
import { ActivityIndicator, FlatList, Text } from 'react-native';
import { ArchivableGalleryItem } from '../components/ArchivableGalleryItem';
import { ListSeparator } from '../components/ListSeparator';
import { Placeholder } from '../components/Placeholder';
import { useAppTheme } from '../hooks/useAppTheme';
import { useArchivableGalleries } from '../hooks/useArchivableGalleries';
import { useBottomInset } from '../hooks/useBottomInset';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/**
 * Galleries to archive: the delivered galleries whose space the photographer can free, the one delivered longest ago first. They
 * are archived by hand here, for example with the photos already backed up on the computer; the app also tells the photographer
 * once, 90 days after the delivery, and archives by itself what is left after 180 days.
 * @returns The screen.
 */
export default function ArchivableGalleriesScreen(): ReactElement {
  const bottomInset = useBottomInset();
  const styles = useStyles();
  const { palette } = useAppTheme();
  const query = useArchivableGalleries();
  // The age of each gallery is counted from when the screen opened; the list is pulled down to refresh, not left open for days.
  const [now] = useState(() => Date.now());
  const items = query.data ?? [];

  let empty: ReactElement;
  if (query.isPending) {
    empty = <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando las galerías" style={styles.loader} />;
  } else if (query.isError) {
    empty = <Placeholder message="No se pudieron cargar las galerías. Revisa la conexión e inténtalo de nuevo." onRetry={() => void query.refetch()} />;
  } else {
    empty = <Placeholder message="No tienes galerías entregadas por archivar." />;
  }

  return (
    <FlatList
      data={items}
      keyExtractor={(gallery) => gallery.id}
      renderItem={({ item }) => <ArchivableGalleryItem gallery={item} now={now} />}
      ItemSeparatorComponent={ListSeparator}
      ListHeaderComponent={
        items.length > 0 ? <Text style={styles.hint}>Al liberar el espacio se borran de forma definitiva las pruebas, los finales y el ZIP. Hazlo cuando ya tengas las fotos en tu computadora.</Text> : null
      }
      ListEmptyComponent={empty}
      refreshing={query.isRefetching}
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
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    marginBottom: spacing.md,
    fontFamily: FONT_FAMILIES.body,
  },
  loader: {
    marginTop: spacing.lg,
  },
}));
