import { ApiError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import { StorageUploadError } from '../api/storageUploadError';
import { ProofRenderError } from '../media/proofRenderer';
import type { ProofRenderFailure, UploadItemStatus } from '../types/media/upload.types';

/** Message for a request that never got an answer. */
const NO_CONNECTION = 'No hay conexión. Revisa tu internet y vuelve a intentarlo.';

/** Message for anything the app did not expect. */
const UNEXPECTED = 'Algo salió mal. Inténtalo de nuevo en un momento.';

/** What each way a photo can fail to become a proof means for the photographer. */
const RENDER_MESSAGES: Readonly<Record<ProofRenderFailure, string>> = {
  decode: 'No se pudo abrir esta foto. ¿Es un JPEG que no está dañado?',
  encode: 'El teléfono no pudo preparar esta foto. Cierra otras apps e inténtalo de nuevo.',
  too_big: 'No se pudo hacer esta foto lo bastante liviana.',
};

/** What each reason the API gives for not registering an upload means for the photographer. */
const REJECTION_MESSAGES: Readonly<Record<string, string>> = {
  missing: 'La foto no llegó completa al almacenamiento.',
  wrong_type: 'La foto no es un JPEG.',
  too_big: 'La foto pesa más de lo permitido.',
  empty: 'La foto llegó vacía.',
  invalid_name: 'El nombre del archivo no es válido.',
  gallery_full: 'La galería ya tiene el máximo de pruebas.',
  not_chosen: 'El cliente no eligió esta foto.',
  locked: 'Este final ya está publicado y no se puede cambiar.',
};

/** Where each photo of the queue is, in words. */
const STATUS_MESSAGES: Readonly<Record<UploadItemStatus, string>> = {
  waiting: 'En espera',
  preparing: 'Preparando…',
  uploading: 'Subiendo…',
  registering: 'Confirmando…',
  failed: 'No se subió',
};

/**
 * Says where a photo of the queue is.
 * @param status Where the photo is.
 * @returns The words.
 */
export function describeUploadStatus(status: UploadItemStatus): string {
  return STATUS_MESSAGES[status];
}

/**
 * Says why a photo could not be turned into a proof.
 * @param error What the renderer threw.
 * @returns The message for the photo.
 */
export function describeRenderFailure(error: unknown): string {
  return error instanceof ProofRenderError ? RENDER_MESSAGES[error.kind] : RENDER_MESSAGES.decode;
}

/**
 * Says why the API did not register an upload.
 * @param reason Stable reason the API gave.
 * @returns The message for the photo.
 */
export function describeRejection(reason: string): string {
  return REJECTION_MESSAGES[reason] ?? 'La foto no fue aceptada.';
}

/**
 * Says why a file could not be sent to the storage.
 * @param error What the upload threw.
 * @returns The message for the file.
 */
export function describeUploadFailure(error: unknown): string {
  if (error instanceof StorageUploadError) {
    if (error.status === 401 || error.status === 403) {
      return 'El permiso para subir esta foto venció. Vuelve a intentarlo.';
    }

    if (error.status === null) {
      return NO_CONNECTION;
    }
  }

  return 'No se pudo subir esta foto. Vuelve a intentarlo.';
}

/**
 * Says why a request about uploading failed: asking for the places, registering, or the session. It says what to do, never what the
 * server said.
 * @param error Error of the request.
 * @returns The message.
 */
export function describeUploadRequestError(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'gallery.full':
        return 'La galería ya tiene el máximo de pruebas.';
      case 'gallery.invalid_state':
        return 'La galería ya no está en un estado que permita subir. Vuelve atrás y revisa su estado.';
      case 'gallery.invalid_photo':
        return 'Alguna de las fotos no es válida: debe ser una que el cliente eligió, en JPEG y de hasta 50 MB.';
      case 'gallery.invalid_zip':
        return 'El ZIP no es válido: debe pesar más de 0 y hasta 500 MB.';
      case 'storage.unavailable':
        return 'El almacenamiento de fotos no está disponible ahora. Inténtalo de nuevo en un momento.';
      case 'rate_limit.exceeded':
        return 'Demasiadas acciones seguidas. Espera un minuto e inténtalo de nuevo.';
      default:
        break;
    }

    if (error.status === 401 || error.status === 403) {
      return 'Tu sesión terminó. Entra de nuevo.';
    }

    if (error.status === 404) {
      return 'No encontramos esta galería.';
    }

    return UNEXPECTED;
  }

  return error instanceof ResponseShapeError ? 'La app y el servidor no se entendieron. Inténtalo de nuevo más tarde.' : NO_CONNECTION;
}

/**
 * Says why the ZIP could not be sent to the storage.
 * @param error What the upload threw.
 * @returns The message.
 */
export function describeZipFailure(error: unknown): string {
  if (error instanceof StorageUploadError && error.status !== null) {
    return error.status === 401 || error.status === 403
      ? 'El permiso para subir el archivo venció. Vuelve a intentarlo.'
      : 'El almacenamiento no aceptó el archivo. Vuelve a intentarlo.';
  }

  return 'Se cortó la subida. Revisa tu internet y vuelve a intentarlo.';
}
