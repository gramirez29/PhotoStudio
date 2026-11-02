import { Alert } from 'react-native';
import type { GalleryResponse } from '../types/api/gallery';
import type { GalleryActionHandlers } from '../types/hooks/useGalleryActions.types';
import type { GalleryCommand } from '../types/hooks/useGalleryCommand.types';
import { galleryErrorMessage } from '../utils/apiErrors';
import { useGalleryCommand } from './useGalleryCommand';

/**
 * Gives the gallery screen what to run when the photographer taps an action. Each action that changes something asks to confirm first
 * with a text that says what will happen, because several of them send an email to the client or cannot be undone.
 * @param gallery The gallery.
 * @returns The handlers, and whether one is running or failed.
 */
export function useGalleryActions(gallery: GalleryResponse): GalleryActionHandlers {
  const command = useGalleryCommand(gallery.id);

  /**
   * Asks to confirm and, if the photographer agrees, runs the command.
   * @param title Title of the question.
   * @param message What will happen.
   * @param confirm Label of the button that agrees.
   * @param toRun The command to run.
   * @param destructive True when what is asked cannot be undone, so the button is shown as dangerous.
   */
  function confirmAnd(title: string, message: string, confirm: string, toRun: GalleryCommand, destructive = false): void {
    Alert.alert(title, message, [
      { text: 'Volver', style: 'cancel' },
      { text: confirm, style: destructive ? 'destructive' : 'default', onPress: () => command.mutate(toRun) },
    ]);
  }

  return {
    isPending: command.isPending,
    errorMessage: command.isError ? galleryErrorMessage(command.error) : null,
    openSelection: () =>
      confirmAnd(
        'Publicar las pruebas',
        'El cliente recibirá un correo y elegirá sus fotos desde el enlace de su reserva. Después de publicar ya no podrás cambiar las pruebas.',
        'Publicar',
        { kind: 'openSelection' },
      ),
    publishFinals: () =>
      confirmAnd(
        'Publicar los finales del paquete',
        'Si el cliente no debe nada, podrá descargarlas de inmediato y recibirá un correo. Si debe saldo, las descargará cuando se verifique su pago. Después ya no podrás cambiar los finales del paquete.',
        'Publicar finales',
        { kind: 'publishFinals' },
      ),
    releaseExtras: () =>
      confirmAnd(
        'Entregar las adicionales',
        '¿El cliente ya pagó las fotos adicionales? Verá las que tengan su final en su galería. Las que aún no tengan final no aparecerán.',
        'Sí, ya pagó',
        { kind: 'releaseExtras' },
      ),
    removeZip: () =>
      confirmAnd(
        'Borrar el ZIP',
        'El cliente ya no podrá descargarlo hasta que subas uno nuevo. Las fotos no se borran.',
        'Borrar el ZIP',
        { kind: 'removeZip' },
        true,
      ),
    release: () =>
      confirmAnd(
        'Liberar espacio',
        '¿Ya descargaste las fotos? Se borran de forma definitiva las pruebas, los finales y el ZIP, y la galería queda archivada: el cliente ya no podrá descargar. No se puede deshacer.',
        'Sí, borrar todo',
        { kind: 'release' },
        true,
      ),
    removeProof: (photoId: string) =>
      confirmAnd('Quitar esta prueba', 'Se quita de la galería. Solo se puede mientras el cliente no haya empezado a elegir.', 'Quitar', { kind: 'removeProof', photoId }, true),
  };
}
