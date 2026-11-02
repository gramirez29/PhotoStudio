import { useState, type ReactElement } from 'react';
import { Text, View } from 'react-native';
import { useDownloadFinal } from '../hooks/useDownloadFinal';
import { shareText } from '../navigation/externalLinks';
import type { GallerySelectionBlockProps } from '../types/components/GalleryComponents.types';
import { namesOf } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { GalleryPhotoTile } from './GalleryPhotoTile';
import { GalleryPhotoViewer } from './GalleryPhotoViewer';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * One block of the selection, the package or the extras: its photos in a grid, how many already have their final, a button that
 * copies or shares the names of the original files (to paste into the search of the editing software) and the viewer of the real
 * photos.
 * @param props Component props.
 * @returns The block.
 */
export function GallerySelectionBlock({ title, hint, photos, galleryId }: GallerySelectionBlockProps): ReactElement {
  const styles = useStyles();
  const [open, setOpen] = useState<number | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const download = useDownloadFinal(galleryId);
  const withFinal = photos.filter((photo) => photo.finalBytes !== null).length;

  return (
    <View style={styles.block}>
      <Text style={styles.title}>
        {title} ({photos.length})
      </Text>
      <Text style={styles.hint}>{hint}</Text>
      <Text style={styles.hint}>
        {withFinal} de {photos.length} con su final
      </Text>
      <ActionButton label="Copiar o compartir los nombres" disabled={photos.length === 0} onPress={() => void shareText(namesOf(photos))} />
      <View style={styles.grid}>
        {photos.map((photo, position) => (
          <GalleryPhotoTile
            key={photo.id}
            photo={photo}
            downloading={download.isPending && downloading === photo.id}
            onOpen={() => setOpen(position)}
            onDownload={() => {
              setDownloading(photo.id);
              download.mutate(photo.id);
            }}
          />
        ))}
      </View>
      {download.isError && <Text style={styles.error}>No pudimos abrir la descarga. Revisa la conexión e inténtalo de nuevo.</Text>}
      {open !== null && <GalleryPhotoViewer galleryId={galleryId} photos={photos} index={open} onMove={setOpen} onClose={() => setOpen(null)} />}
    </View>
  );
}

/** Styles of the block. */
const useStyles = makeStyles((palette) => ({
  block: {
    gap: spacing.sm,
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
    justifyContent: 'space-between',
  },
  error: {
    color: palette.error,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
}));
