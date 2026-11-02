import { useState, type ReactElement } from 'react';
import { ActivityIndicator, Text } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { useGalleryProofs } from '../hooks/useGallery';
import type { GalleryProofsProps } from '../types/components/GalleryComponents.types';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { GalleryProofGrid } from './GalleryProofGrid';
import { GalleryProofUpload } from './GalleryProofUpload';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * The proofs of the gallery. The grid is loaded only when the photographer asks to see it, because it is many images and the phone
 * may be on mobile data. Uploading proofs is done from the computer for now.
 * @param props Component props.
 * @returns The section.
 */
export function GalleryProofs({ gallery, handlers }: GalleryProofsProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const [shown, setShown] = useState(false);
  const query = useGalleryProofs(gallery.id, shown);
  const canRemove = gallery.allowedActions.includes('RemoveProof');

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>
        Pruebas para el cliente ({gallery.photoCount} de {gallery.maxPhotos})
      </Text>
      {gallery.photoCount === 0 ? (
        <Text style={styles.body}>Todavía no hay pruebas. Súbelas desde aquí o desde la computadora.</Text>
      ) : (
        <ActionButton label={shown ? 'Ocultar las pruebas' : 'Ver las pruebas'} onPress={() => setShown((current) => !current)} />
      )}
      {gallery.allowedActions.includes('UploadProofs') && <GalleryProofUpload gallery={gallery} />}
      {shown && query.isPending && <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando las pruebas" />}
      {shown && query.isError && (
        <>
          <Text style={styles.error}>No se pudieron cargar las pruebas. Revisa la conexión e inténtalo de nuevo.</Text>
          <ActionButton label="Reintentar" onPress={() => void query.refetch()} />
        </>
      )}
      {shown && query.data !== undefined && (
        <GalleryProofGrid proofs={query.data} onRemove={canRemove && !handlers.isPending ? handlers.removeProof : undefined} />
      )}
    </AppCard>
  );
}

/** Styles of the section. */
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
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
