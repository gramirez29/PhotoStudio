import type { ReactElement } from 'react';
import { ScrollView, Text } from 'react-native';
import { useBottomInset } from '../hooks/useBottomInset';
import { useGalleryActions } from '../hooks/useGalleryActions';
import type { GalleryDetailsProps } from '../types/components/GalleryComponents.types';
import { formatDateTime } from '../utils/format';
import { describeGalleryStatus, describeTerms } from '../utils/gallery';
import { AppCard } from './AppCard';
import { GalleryActions } from './GalleryActions';
import { GalleryProofs } from './GalleryProofs';
import { GallerySelection } from './GallerySelection';
import { GalleryStatusPill } from './GalleryStatusPill';
import { GalleryZipCard } from './GalleryZipCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * The gallery of a session on the phone: who it is for and where it is, the steps the photographer can take, the proofs, what the
 * client chose as a grid of photos and the ZIP. Everything the web of the photographer does about a gallery except uploading files,
 * which needs the computer for now.
 * @param props Component props.
 * @returns The screen.
 */
export function GalleryDetails({ gallery }: GalleryDetailsProps): ReactElement {
  const styles = useStyles();
  const bottomInset = useBottomInset();
  const handlers = useGalleryActions(gallery);

  return (
    <ScrollView contentContainerStyle={[styles.container, { paddingBottom: spacing.md + bottomInset }]}>
      <GalleryStatusPill gallery={gallery} />
      <Text style={styles.title}>{gallery.clientName}</Text>
      <Text style={styles.subtitle}>{gallery.packageName}</Text>
      <Text style={styles.subtitle}>{formatDateTime(gallery.sessionStart)}</Text>
      <AppCard style={styles.card}>
        <Text style={styles.body}>{describeGalleryStatus(gallery.status).hint}</Text>
        <Text style={styles.muted}>{describeTerms(gallery)}</Text>
        {gallery.selectionReopenedAt !== null && (
          <Text style={styles.muted}>
            Reabierta el {formatDateTime(gallery.selectionReopenedAt)}
            {gallery.selectionReopenReason === null ? '' : `: ${gallery.selectionReopenReason}`}
          </Text>
        )}
      </AppCard>
      <GalleryActions gallery={gallery} handlers={handlers} />
      <GalleryProofs gallery={gallery} handlers={handlers} />
      <GallerySelection gallery={gallery} />
      <GalleryZipCard gallery={gallery} handlers={handlers} />
    </ScrollView>
  );
}

/** Styles of the screen. */
const useStyles = makeStyles((palette) => ({
  container: {
    padding: spacing.md,
    gap: spacing.sm,
  },
  title: {
    color: palette.onSurface,
    fontSize: 22,
    marginTop: spacing.sm,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  subtitle: {
    color: palette.onSurfaceVariant,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  card: {
    gap: spacing.xs,
    marginTop: spacing.md,
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
