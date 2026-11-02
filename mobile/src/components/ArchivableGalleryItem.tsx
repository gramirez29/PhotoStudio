import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useGalleryActions } from '../hooks/useGalleryActions';
import { openGallery } from '../navigation/appNavigation';
import type { ArchivableGalleryItemProps } from '../types/components/GalleryComponents.types';
import { describeDeliveryAge } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * One delivered gallery of the list of the ones whose space can be freed: the client, the package, how long ago it was delivered, a
 * way to open it and the button that frees the space (it asks to confirm first, because the files are deleted for good).
 * @param props Component props.
 * @returns The card.
 */
export function ArchivableGalleryItem({ gallery, now }: ArchivableGalleryItemProps): ReactElement {
  const styles = useStyles();
  const handlers = useGalleryActions(gallery);

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>{gallery.clientName}</Text>
      <Text style={styles.detail}>{gallery.packageName}</Text>
      <Text style={styles.detail}>{describeDeliveryAge(gallery, now)}</Text>
      {handlers.errorMessage !== null && <Text style={styles.error}>{handlers.errorMessage}</Text>}
      <ActionButton label="Abrir la galería" onPress={() => openGallery(gallery.id)} />
      <ActionButton label="Liberar espacio" variant="danger" disabled={handlers.isPending} onPress={handlers.release} />
    </AppCard>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.xs,
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
