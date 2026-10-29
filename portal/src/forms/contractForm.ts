import type { ContractFormResult, ContractFormValues } from '../types/forms/contractForm.types';

/** Most characters of the name of the signer. */
export const MAX_SIGNER_NAME_LENGTH = 100;

/** Most characters of the place of the session (the backend enforces the same limit). */
export const MAX_LOCATION_LENGTH = 200;

/** Fewest people a session can declare (the backend enforces the same limit). */
export const MIN_PARTICIPANTS = 1;

/** Most people a session can declare (the backend enforces the same limit). */
export const MAX_PARTICIPANTS = 100;

/** The form before the client types anything. */
export const INITIAL_CONTRACT_VALUES: ContractFormValues = {
  signerName: '',
  idNumber: '',
  participantCount: '',
  sessionLocation: '',
  imageUse: null,
  acceptsTerms: false,
};

/**
 * Keeps the digits of an identity number typed the way people write it, for example `1-1234-5678`.
 * @param typed What the client typed.
 * @returns The digits when the text only has digits, hyphens and spaces; otherwise null.
 */
export function readIdDigits(typed: string): string | null {
  const stripped = typed.replace(/[-\s]/g, '');
  return /^\d+$/.test(stripped) ? stripped : null;
}

/**
 * Validates the contract form and, when it is valid, builds the request. The backend enforces the same rules; checking them
 * here gives a message next to the field without a round trip.
 * @param values What the client typed and chose.
 * @param templateVersion Version of the contract the client read; it travels back so they sign what they read.
 * @returns The request, or the validation messages by field.
 */
export function validateContractForm(values: ContractFormValues, templateVersion: string): ContractFormResult {
  const errors: { -readonly [Field in keyof ContractFormValues]?: string } = {};

  const signerName = values.signerName.trim();
  if (signerName.length === 0) {
    errors.signerName = 'Escribe tu nombre completo.';
  } else if (signerName.length > MAX_SIGNER_NAME_LENGTH) {
    errors.signerName = `El nombre no puede tener más de ${MAX_SIGNER_NAME_LENGTH} caracteres.`;
  }

  const idDigits = readIdDigits(values.idNumber);
  if (idDigits === null || ![9, 11, 12].includes(idDigits.length)) {
    errors.idNumber = 'Escribe tu cédula (9 dígitos) o tu DIMEX (11 o 12 dígitos).';
  }

  const typedCount = values.participantCount.trim();
  const participants = /^\d+$/.test(typedCount) ? Number(typedCount) : Number.NaN;
  if (!Number.isInteger(participants) || participants < MIN_PARTICIPANTS || participants > MAX_PARTICIPANTS) {
    errors.participantCount = `Escribe cuántas personas vienen a la sesión (de ${MIN_PARTICIPANTS} a ${MAX_PARTICIPANTS}).`;
  }

  const location = values.sessionLocation.trim();
  if (location.length > MAX_LOCATION_LENGTH) {
    errors.sessionLocation = `El lugar no puede tener más de ${MAX_LOCATION_LENGTH} caracteres.`;
  }

  if (values.imageUse === null) {
    errors.imageUse = 'Elige si autorizas o no el uso de las fotos.';
  }

  if (!values.acceptsTerms) {
    errors.acceptsTerms = 'Debes aceptar el contrato para firmarlo.';
  }

  if (Object.keys(errors).length > 0 || idDigits === null || values.imageUse === null) {
    return { ok: false, errors };
  }

  return {
    ok: true,
    request: {
      templateVersion,
      signerName,
      idNumber: idDigits,
      participantCount: participants,
      ...(location.length > 0 ? { sessionLocation: location } : {}),
      imageUse: values.imageUse,
      acceptsTerms: true,
    },
  };
}
