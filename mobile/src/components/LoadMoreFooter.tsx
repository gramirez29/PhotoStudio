import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { LoadMoreFooterProps } from '../types/components/LoadMoreFooter.types';
import { ActionButton } from './ActionButton';

/**
 * End of a paged list: the "Ver más" button that loads the next page. It shows nothing when the list has no more pages, says
 * so while the page loads, and offers to try again, with the reason, when loading failed.
 * @param props Component props.
 * @returns The footer, or nothing when there are no more pages.
 */
export function LoadMoreFooter({ hasMore, isLoading, failed, onLoadMore }: LoadMoreFooterProps): ReactElement | null {
  const styles = useStyles();
  useAppTheme();

  if (!hasMore) {
    return null;
  }

  let label = 'Ver más';
  if (isLoading) {
    label = 'Cargando…';
  } else if (failed) {
    label = 'Reintentar';
  }

  return (
    <View style={styles.footer}>
      {failed && !isLoading && <Text style={styles.error}>No se pudo cargar más. Revisa la conexión e inténtalo de nuevo.</Text>}
      <ActionButton label={label} onPress={onLoadMore} disabled={isLoading} />
    </View>
  );
}

/** Styles of the footer. */
const useStyles = makeStyles((palette) => ({
  footer: {
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
