import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useState, type ReactElement } from 'react';
import { ActivityIndicator, Image, Pressable, Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import type { GalleryPhotoTileProps } from '../types/components/GalleryComponents.types';
import { describeFinalLines } from '../utils/gallery';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * One photo of the selection as a card: its small image on top (the thumbnail of the final or, while it has none, the proof), a label
 * that says whether it already has its final, its name and what is known about it under the image, and the download icon inside the
 * same card. Tapping the image opens the photo large. When there is no image, or it fails to load, an icon takes its place, so the
 * card never shows an empty gap and the grid keeps its shape.
 * @param props Component props.
 * @returns The card.
 */
export function GalleryPhotoTile({ photo, onOpen, onDownload, downloading }: GalleryPhotoTileProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const [failed, setFailed] = useState(false);
  const hasFinal = photo.finalBytes !== null;
  const image = photo.thumbUrl !== null && !failed ? photo.thumbUrl : null;

  return (
    <View style={styles.card}>
      <Pressable accessibilityRole="button" accessibilityLabel={`Ver ${photo.name}`} onPress={onOpen} style={styles.media}>
        {image === null ? (
          <View style={styles.placeholder}>
            <MaterialCommunityIcons name="image-outline" size={36} color={palette.onSurfaceVariant} />
          </View>
        ) : (
          <Image source={{ uri: image }} style={styles.image} resizeMode="cover" onError={() => setFailed(true)} accessibilityIgnoresInvertColors />
        )}
        <View style={[styles.badge, !hasFinal && styles.badgeMissing]}>
          <Text style={styles.badgeText}>{hasFinal ? 'Final' : 'Sin final'}</Text>
        </View>
      </Pressable>
      <View style={styles.footer}>
        <View style={styles.text}>
          <Text style={styles.name} numberOfLines={2}>
            {photo.name}
          </Text>
          {describeFinalLines(photo).map((line, position) => (
            <Text key={position} style={styles.info}>
              {line}
            </Text>
          ))}
        </View>
        {hasFinal && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`Descargar el final de ${photo.name}`}
            accessibilityState={{ disabled: downloading }}
            disabled={downloading}
            onPress={onDownload}
            style={[styles.action, downloading && styles.actionBusy]}
          >
            {downloading ? <ActivityIndicator size="small" color={palette.onBrand} /> : <MaterialCommunityIcons name="download" size={22} color={palette.onBrand} />}
          </Pressable>
        )}
      </View>
    </View>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    backgroundColor: palette.background,
    borderColor: palette.outlineVariant,
    borderRadius: 14,
    borderWidth: 1,
    overflow: 'hidden',
    width: '48%',
  },
  media: {
    aspectRatio: 4 / 3,
    backgroundColor: palette.surfaceVariant,
  },
  image: {
    height: '100%',
    width: '100%',
  },
  placeholder: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
  },
  badge: {
    backgroundColor: palette.brand,
    borderRadius: 999,
    left: spacing.xs,
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    position: 'absolute',
    top: spacing.xs,
  },
  badgeMissing: {
    backgroundColor: palette.warning,
  },
  badgeText: {
    color: palette.onBrand,
    fontSize: 11,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  footer: {
    alignItems: 'flex-start',
    flexDirection: 'row',
    gap: spacing.xs,
    justifyContent: 'space-between',
    paddingBottom: spacing.xs,
    paddingLeft: spacing.sm,
    paddingRight: spacing.xs,
    paddingTop: spacing.xs,
  },
  text: {
    flex: 1,
    gap: 2,
  },
  name: {
    color: palette.onSurface,
    fontSize: 12,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  info: {
    color: palette.onSurfaceVariant,
    fontSize: 11,
    lineHeight: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  action: {
    alignItems: 'center',
    backgroundColor: palette.brand,
    borderRadius: 999,
    height: 40,
    justifyContent: 'center',
    width: 40,
  },
  actionBusy: {
    opacity: 0.6,
  },
}));
