import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { openGalleryReopen, openGalleryTerms } from '../navigation/appNavigation';
import type { GalleryActionsProps } from '../types/components/GalleryComponents.types';
import { ActionButton } from './ActionButton';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * The steps the photographer can take with the gallery right now. It shows only what the API says is allowed (`allowedActions`) and
 * never decides it by itself; each step that changes something asks to confirm first.
 * @param props Component props.
 * @returns The card, or nothing when there is nothing to do.
 */
export function GalleryActions({ gallery, handlers }: GalleryActionsProps): ReactElement | null {
  const styles = useStyles();
  const can = (action: GalleryActionsProps['gallery']['allowedActions'][number]): boolean => gallery.allowedActions.includes(action);
  const buttons: ReactElement[] = [];

  if (can('EditTerms')) {
    buttons.push(<ActionButton key="terms" label="Fotos incluidas, de regalo y precio" onPress={() => openGalleryTerms(gallery.id)} />);
  }

  if (can('OpenSelection')) {
    buttons.push(<ActionButton key="open" label="Publicar las pruebas" disabled={handlers.isPending} onPress={handlers.openSelection} />);
  }

  if (can('PublishFinals')) {
    buttons.push(<ActionButton key="publish" label="Publicar los finales del paquete" disabled={handlers.isPending} onPress={handlers.publishFinals} />);
  }

  if (can('ReleaseExtras')) {
    buttons.push(<ActionButton key="extras" label="Entregar las adicionales" disabled={handlers.isPending} onPress={handlers.releaseExtras} />);
  }

  if (can('ReopenSelection')) {
    buttons.push(<ActionButton key="reopen" label="Reabrir la selección" variant="danger" disabled={handlers.isPending} onPress={() => openGalleryReopen(gallery.id)} />);
  }

  if (buttons.length === 0 && handlers.errorMessage === null) {
    return null;
  }

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Acciones</Text>
      {buttons}
      {handlers.errorMessage !== null && <Text style={styles.error}>{handlers.errorMessage}</Text>}
    </AppCard>
  );
}

/** Styles of the card. */
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
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
