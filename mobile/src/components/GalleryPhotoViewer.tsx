import type { ReactElement } from 'react';
import { ActivityIndicator, Image, Modal, Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { useFinalView } from '../hooks/useGallery';
import type { GalleryPhotoViewerProps } from '../types/components/GalleryComponents.types';
import { describeFinal } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * One photo, large, in the quality it was edited: the real file, not the small image. Only the photo that is open is loaded, so the
 * phone never holds many photos of several megabytes. While it arrives the small image is shown, and a photo without a final shows its
 * proof, which is what there is.
 * @param props Component props.
 * @returns The viewer, or nothing when the position is not a photo.
 */
export function GalleryPhotoViewer({ galleryId, photos, index, onMove, onClose }: GalleryPhotoViewerProps): ReactElement | null {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const photo = photos[index];
  const view = useFinalView(galleryId, photo?.id ?? '', photo?.finalBytes !== null && photo !== undefined);

  if (photo === undefined) {
    return null;
  }

  const source = view.data ?? photo.thumbUrl;

  return (
    <Modal visible animationType="fade" onRequestClose={onClose} statusBarTranslucent>
      <View style={styles.screen} accessibilityViewIsModal>
        <View style={styles.frame}>
          {source !== null && <Image source={{ uri: source }} style={styles.image} resizeMode="contain" accessibilityLabel={photo.name} accessibilityIgnoresInvertColors />}
          {view.isPending && photo.finalBytes !== null && <ActivityIndicator color={palette.onPrimary} style={styles.loader} accessibilityLabel="Cargando la foto en su calidad real" />}
        </View>
        <Text style={styles.caption}>
          {photo.name} · {photo.finalBytes === null ? 'prueba, todavía sin final' : describeFinal(photo)}
        </Text>
        {view.isError && <Text style={styles.error}>No pudimos cargar la foto en su calidad real. Se muestra la imagen pequeña.</Text>}
        <View style={styles.bar}>
          <ActionButton label="Anterior" disabled={index === 0} onPress={() => onMove(index - 1)} />
          <ActionButton label="Siguiente" disabled={index >= photos.length - 1} onPress={() => onMove(index + 1)} />
          <ActionButton label="Cerrar" onPress={onClose} />
        </View>
      </View>
    </Modal>
  );
}

/** Styles of the viewer. */
const useStyles = makeStyles((palette) => ({
  screen: {
    backgroundColor: palette.scrim,
    flex: 1,
    gap: spacing.sm,
    padding: spacing.md,
  },
  frame: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
  },
  image: {
    height: '100%',
    width: '100%',
  },
  loader: {
    position: 'absolute',
  },
  caption: {
    color: palette.onPrimary,
    fontSize: 13,
    textAlign: 'center',
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.warning,
    fontSize: 13,
    textAlign: 'center',
    fontFamily: FONT_FAMILIES.body,
  },
  bar: {
    gap: spacing.xs,
  },
}));
