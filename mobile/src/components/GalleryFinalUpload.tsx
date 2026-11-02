import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useFinalUpload } from '../hooks/useFinalUpload';
import type { GalleryFinalUploadProps } from '../types/components/UploadComponents.types';
import { ActionButton } from './ActionButton';
import { UploadQueueList } from './UploadQueueList';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Uploads the final photos from the phone. The photographer chooses the edited photos from the library; the app pairs each with the
 * photo the client chose by its file name (`IMG_0001-Edit.jpg` goes with `IMG_0001`), says which ones it did not take, and after
 * confirming sends them as they are, with their thumbnails.
 * @param props Component props.
 * @returns The section.
 */
export function GalleryFinalUpload({ gallery, photos }: GalleryFinalUploadProps): ReactElement {
  const styles = useStyles();
  const upload = useFinalUpload(gallery, photos);
  const busy = upload.preparing || upload.queue.running;

  return (
    <>
      {upload.pending.length === 0 ? (
        <ActionButton label={upload.preparing ? 'Buscando…' : 'Subir finales desde el teléfono'} disabled={busy} onPress={() => void upload.choose()} />
      ) : (
        <>
          <Text style={styles.body}>
            {upload.pending.length === 1 ? 'Coincide 1 archivo con una foto que eligió el cliente:' : `Coinciden ${upload.pending.length} archivos con fotos que eligió el cliente:`}
          </Text>
          {upload.pending.map((match) => (
            <Text key={match.photoId} style={styles.match} numberOfLines={1}>
              {match.file.name} → {match.photoName}
            </Text>
          ))}
          <ActionButton label={upload.pending.length === 1 ? 'Subir 1 final' : `Subir ${upload.pending.length} finales`} onPress={upload.confirm} />
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
  body: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  match: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  notice: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
}));
