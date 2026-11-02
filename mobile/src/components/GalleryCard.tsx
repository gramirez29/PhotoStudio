import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useGalleryOfBooking } from '../hooks/useGallery';
import { openGallery } from '../navigation/appNavigation';
import type { GalleryCardProps } from '../types/components/GalleryComponents.types';
import { describeGalleryStatus, describeTerms } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { GalleryStatusPill } from './GalleryStatusPill';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * Card of the booking detail with the gallery of the session: where it is, what the package includes and how many photos the client
 * chose, with the way to open it. It shows nothing for a booking without a gallery, which is every booking whose session did not
 * happen: the gallery is created by the system when the session is completed.
 * @param props Component props.
 * @returns The card, or nothing.
 */
export function GalleryCard({ booking }: GalleryCardProps): ReactElement | null {
  const styles = useStyles();
  const query = useGalleryOfBooking(booking.id, booking.status === 'Completed');
  const gallery = query.data;

  if (gallery === undefined || gallery === null) {
    return null;
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Galería de fotos</Text>
      <GalleryStatusPill gallery={gallery} />
      <Text style={styles.body}>{describeGalleryStatus(gallery.status).hint}</Text>
      <Text style={styles.muted}>{describeTerms(gallery)}</Text>
      {gallery.selectionSubmittedAt !== null && (
        <Text style={styles.body}>
          El cliente eligió {gallery.selectedPhotoCount} {gallery.selectedPhotoCount === 1 ? 'foto' : 'fotos'}
          {gallery.extraPhotoCount > 0 ? ` (${gallery.extraPhotoCount} ${gallery.extraPhotoCount === 1 ? 'adicional' : 'adicionales'})` : ''}.
        </Text>
      )}
      <ActionButton label="Abrir la galería" onPress={() => openGallery(gallery.id)} />
    </AppCard>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  body: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  muted: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
}));
