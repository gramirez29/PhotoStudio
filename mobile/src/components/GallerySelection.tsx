import type { ReactElement } from 'react';
import { ActivityIndicator, Text } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { useGallerySelection } from '../hooks/useGallery';
import type { GallerySelectionProps } from '../types/components/GalleryComponents.types';
import { formatDateTime, formatMoney } from '../utils/format';
import { splitSelection } from '../utils/gallery';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { GallerySelectionBlock } from './GallerySelectionBlock';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * What the client chose, in two blocks: the photos of the package and the extra ones, so the photographer tells at a glance which he
 * must edit for the package and which are agreed on apart. It says so when the client has not sent their selection yet.
 * @param props Component props.
 * @returns The section.
 */
export function GallerySelection({ gallery }: GallerySelectionProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const sent = gallery.selectionSubmittedAt !== null;
  const query = useGallerySelection(gallery.id, sent);

  let content: ReactElement;
  if (!sent) {
    content = <Text style={styles.body}>El cliente todavía no ha enviado su selección.</Text>;
  } else if (query.isPending) {
    content = <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando la selección del cliente" />;
  } else if (query.isError) {
    content = (
      <>
        <Text style={styles.error}>No se pudo cargar la selección. Revisa la conexión e inténtalo de nuevo.</Text>
        <ActionButton label="Reintentar" onPress={() => void query.refetch()} />
      </>
    );
  } else {
    const { packagePhotos, extraPhotos } = splitSelection(query.data);
    content = (
      <>
        <Text style={styles.body}>
          {query.data.photos.length} {query.data.photos.length === 1 ? 'foto elegida' : 'fotos elegidas'}
          {query.data.submittedAt === null ? '' : ` · enviada el ${formatDateTime(query.data.submittedAt)}`}
        </Text>
        <GallerySelectionBlock
          title="Fotos del paquete"
          hint="Son las que cubre el paquete contratado: se editan y se entregan con el saldo en cero."
          photos={packagePhotos}
          galleryId={gallery.id}
        />
        {extraPhotos.length > 0 && (
          <GallerySelectionBlock
            title="Fotos adicionales"
            hint={`Fuera del paquete: ${formatMoney(query.data.extraTotal)} en total. Se acuerdan con el cliente; edítalas y entrégalas solo cuando pague.`}
            photos={extraPhotos}
            galleryId={gallery.id}
          />
        )}
      </>
    );
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Selección del cliente</Text>
      {content}
    </AppCard>
  );
}

/** Styles of the section. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.md,
    marginTop: spacing.md,
  },
  title: {
    color: palette.onSurface,
    fontSize: 17,
    fontFamily: FONT_FAMILIES.titleBold,
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
