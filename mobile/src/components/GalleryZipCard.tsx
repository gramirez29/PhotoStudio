import type { ReactElement } from 'react';
import { Text } from 'react-native';
import type { GalleryZipCardProps } from '../types/components/GalleryComponents.types';
import { describeZip } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { GalleryZipUpload } from './GalleryZipUpload';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * The ZIP with the photos, which the photographer makes by hand and uploads from the computer: its size, and whether and when the
 * client downloaded it. It can be deleted from here, for example to upload another when the client asks again. It shows nothing
 * while there is no ZIP.
 * @param props Component props.
 * @returns The card, or nothing.
 */
export function GalleryZipCard({ gallery, handlers }: GalleryZipCardProps): ReactElement | null {
  const styles = useStyles();

  const canUpload = gallery.allowedActions.includes('UploadZip');
  if (gallery.zip === null && !canUpload) {
    return null;
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>ZIP con las fotos</Text>
      {canUpload && <GalleryZipUpload gallery={gallery} />}
      {gallery.zip !== null && describeZip(gallery.zip).map((line) => (
        <Text key={line} style={styles.body}>
          {line}
        </Text>
      ))}
      {gallery.allowedActions.includes('RemoveZip') && <ActionButton label="Borrar el ZIP" variant="danger" disabled={handlers.isPending} onPress={handlers.removeZip} />}
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
}));
