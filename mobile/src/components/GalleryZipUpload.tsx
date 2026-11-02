import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useZipUpload } from '../hooks/useZipUpload';
import type { GalleryZipUploadProps } from '../types/components/UploadComponents.types';
import { formatBytes } from '../utils/format';
import { ActionButton } from './ActionButton';
import { UploadProgressBar } from './UploadProgressBar';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Uploads the ZIP with the photos from the phone. The photographer chooses the ZIP from the files of the phone, confirms, and the file
 * goes straight to the storage with a bar that shows how much was sent; the app has to stay open until it ends.
 * @param props Component props.
 * @returns The section.
 */
export function GalleryZipUpload({ gallery }: GalleryZipUploadProps): ReactElement {
  const styles = useStyles();
  const upload = useZipUpload(gallery);
  const replaces = gallery.zip !== null;

  return (
    <>
      {upload.file === null ? (
        <ActionButton label={replaces ? 'Subir otro ZIP' : 'Subir el ZIP desde el teléfono'} onPress={() => void upload.choose()} />
      ) : (
        <>
          <Text style={styles.body}>
            {upload.file.name} · {formatBytes(upload.file.bytes)}
          </Text>
          {replaces && !upload.uploading && <Text style={styles.notice}>Reemplaza al ZIP que hay ahora y su contador de descargas empieza de nuevo.</Text>}
          {upload.uploading ? (
            <>
              <Text style={styles.notice}>Subiendo… {Math.round(upload.progress * 100)} %. Deja la app abierta hasta que termine.</Text>
              <UploadProgressBar fraction={upload.progress} accessibilityLabel="Subiendo el ZIP" />
            </>
          ) : (
            <ActionButton label="Subir este ZIP" onPress={upload.confirm} />
          )}
          <ActionButton label={upload.uploading ? 'Detener la subida' : 'Cancelar'} variant="danger" onPress={upload.cancel} />
        </>
      )}
      {upload.problem !== null && <Text style={styles.problem}>{upload.problem}</Text>}
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
  notice: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  problem: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
