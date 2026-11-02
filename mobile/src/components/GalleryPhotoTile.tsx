import type { ReactElement } from 'react';
import { Image, Pressable, Text, View } from 'react-native';
import type { GalleryPhotoTileProps } from '../types/components/GalleryComponents.types';
import { describeFinal } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * One photo of the selection in the grid: its small image (the thumbnail of the final or, while it has none, the proof), a label that
 * says whether it already has its final, its name and what is known about it, and the button to download the final. Tapping the photo
 * opens it large.
 * @param props Component props.
 * @returns The tile.
 */
export function GalleryPhotoTile({ photo, onOpen, onDownload, downloading }: GalleryPhotoTileProps): ReactElement {
  const styles = useStyles();
  const hasFinal = photo.finalBytes !== null;

  return (
    <View style={styles.tile}>
      <Pressable accessibilityRole="button" accessibilityLabel={`Ver ${photo.name}`} onPress={onOpen} style={styles.photo}>
        {photo.thumbUrl !== null && <Image source={{ uri: photo.thumbUrl }} style={styles.image} resizeMode="cover" accessibilityIgnoresInvertColors />}
        <View style={[styles.badge, !hasFinal && styles.badgeMissing]}>
          <Text style={styles.badgeText}>{hasFinal ? 'Final' : 'Sin final'}</Text>
        </View>
      </Pressable>
      <Text style={styles.name} numberOfLines={1}>
        {photo.name}
      </Text>
      <Text style={styles.info}>{describeFinal(photo)}</Text>
      {hasFinal && <ActionButton label={downloading ? 'Preparando…' : '⬇ Descargar'} disabled={downloading} onPress={onDownload} />}
    </View>
  );
}

/** Styles of the tile. */
const useStyles = makeStyles((palette) => ({
  tile: {
    gap: 4,
    width: '48%',
  },
  photo: {
    aspectRatio: 3 / 2,
    backgroundColor: palette.surfaceVariant,
    borderRadius: 10,
    overflow: 'hidden',
  },
  image: {
    height: '100%',
    width: '100%',
  },
  badge: {
    backgroundColor: palette.brand,
    borderRadius: 999,
    left: 6,
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    position: 'absolute',
    top: 6,
  },
  badgeMissing: {
    backgroundColor: palette.warning,
  },
  badgeText: {
    color: palette.onBrand,
    fontSize: 12,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  name: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  info: {
    color: palette.onSurfaceVariant,
    fontSize: 12,
    fontFamily: FONT_FAMILIES.body,
  },
}));
