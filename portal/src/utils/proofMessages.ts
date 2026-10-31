import { ProofUploadError } from '../api/proofStorage';
import { ApiError, NetworkError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import { ProofRenderError } from '../media/proofRenderer';
import type { ProofRenderFailure } from '../types/media/proofRenderer.types';
import type { UploadItemStatus } from '../types/media/proofUploader.types';

/** Message for a request that never got an answer. */
const NO_CONNECTION = 'No hay conexión. Revisa tu internet y vuelve a intentarlo.';

/** Message for anything the page did not expect. */
const UNEXPECTED = 'Algo salió mal. Inténtalo de nuevo en un momento.';

/** What each way a photo can fail to become a proof means for the photographer. */
const RENDER_MESSAGES: Readonly<Record<ProofRenderFailure, string>> = {
  decode: 'No se pudo abrir esta foto. ¿Es un JPEG que no está dañado?',
  canvas: 'Tu navegador no pudo preparar esta foto. Prueba con otro navegador.',
  too_big: 'No se pudo hacer esta foto lo bastante liviana.',
};

/** What each reason the API gives for not registering a proof means for the photographer. */
const REJECTION_MESSAGES: Readonly<Record<string, string>> = {
  missing: 'La foto no llegó completa al almacenamiento.',
  wrong_type: 'La foto no es un JPEG.',
  too_big: 'La foto pesa más de lo permitido.',
  empty: 'La foto llegó vacía.',
  invalid_name: 'El nombre del archivo no es válido.',
  gallery_full: 'La galería ya tiene el máximo de pruebas.',
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
 * Says why the API did not register a proof.
 * @param reason Stable reason the API gave.
 * @returns The message for the photo.
 */
export function describeRejection(reason: string): string {
  return REJECTION_MESSAGES[reason] ?? 'La foto no fue aceptada.';
}

/**
 * Says why a proof could not be sent to the storage.
 * @param error What the upload threw.
 * @returns The message for the photo.
 */
export function describeUploadFailure(error: unknown): string {
  if (error instanceof NetworkError) {
    return NO_CONNECTION;
  }

  if (error instanceof ProofUploadError && (error.status === 401 || error.status === 403)) {
    return 'El permiso para subir esta foto venció. Vuelve a intentarlo.';
  }

  return 'No se pudo subir esta foto. Vuelve a intentarlo.';
}

/**
 * Says why a request about the proofs failed: asking for the places to upload, registering, looking at them, removing one or
 * opening the selection. It says what to do, never what the server said.
 * @param error Error of the request.
 * @returns The message.
 */
export function describeProofRequestError(error: unknown): string {
  if (error instanceof NetworkError) {
    return NO_CONNECTION;
  }

  if (error instanceof ApiError) {
    switch (error.code) {
      case 'gallery.full':
        return 'La galería ya tiene el máximo de pruebas.';
      case 'gallery.no_photos':
        return 'Sube al menos una prueba antes de publicarlas.';
      case 'gallery.invalid_state':
        return 'Las pruebas ya no se pueden cambiar: el cliente ya puede elegir.';
      case 'gallery.invalid_photo':
        return 'Alguna de las fotos no es válida.';
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
  }

  return error instanceof ResponseShapeError ? 'La página y el servidor no se entendieron. Inténtalo de nuevo más tarde.' : UNEXPECTED;
}
