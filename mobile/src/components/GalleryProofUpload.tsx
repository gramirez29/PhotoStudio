import type { ReactElement } from 'react';
import { Image, Text } from 'react-native';
import { useProofUpload } from '../hooks/useProofUpload';
import type { GalleryProofUploadProps } from '../types/components/UploadComponents.types';
import { ActionButton } from './ActionButton';
import { UploadQueueList } from './UploadQueueList';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * Uploads proofs from the phone. The photographer chooses photos from the library and sees a sample of how the client will see them
 * (the first photo, reduced and with the logo across it) before anything is sent; after confirming, every photo is prepared on the
 * phone and goes up with its progress.
 * @param props Component props.
 * @returns The section.
 */
export function GalleryProofUpload({ gallery }: GalleryProofUploadProps): ReactElement {
  const styles = useStyles();
  const upload = useProofUpload(gallery);
  const busy = upload.preparing || upload.queue.running;

  return (
    <>
      {upload.pending.length === 0 ? (
        <ActionButton label={upload.preparing ? 'Preparando…' : 'Subir pruebas desde el teléfono'} disabled={busy} onPress={() => void upload.choose()} />
      ) : (
        <>
          <Text style={styles.title}>Así verá tu cliente las pruebas</Text>
          {upload.sample !== null && <Image source={{ uri: upload.sample }} style={styles.sample} resizeMode="contain" accessibilityLabel="Muestra de una prueba" />}
          <Text style={styles.body}>
            {upload.pending.length === 1 ? 'Se subirá 1 foto.' : `Se subirán ${upload.pending.length} fotos.`} Quedan en baja calidad y con marca de agua; tus originales no salen del teléfono.
          </Text>
          <ActionButton label={upload.pending.length === 1 ? 'Subir 1 foto' : `Subir ${upload.pending.length} fotos`} onPress={upload.confirm} />
          <ActionButton label="Cancelar" variant="danger" onPress={upload.cancel} />
        </>
      )}
      {upload.notice !== null && <Text style={styles.notice}>{upload.notice}</Text>}
      <UploadQueueList queue={upload.queue} />
    </>
  );
}

/** Styles of the section. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  sample: {
    width: '100%',
    height: 260,
    borderRadius: spacing.xs,
    backgroundColor: palette.surfaceVariant,
  },
  body: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  notice: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
}));
