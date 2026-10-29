import { CONTRACT_TEMPLATE_VERSION } from '../constants/contract';
import type { ContractFormErrors, ContractFormResult, ContractFormValues } from '../types/forms/contractForm.types';

/** Most characters of the name of the person who signs. */
export const MAX_SIGNER_NAME_LENGTH = 100;

/** Most characters of the place of the session (the backend enforces the same limit). */
export const MAX_LOCATION_LENGTH = 200;

/** Fewest people a session can declare (the backend enforces the same limit). */
export const MIN_PARTICIPANTS = 1;

/** Most people a session can declare (the backend enforces the same limit). */
export const MAX_PARTICIPANTS = 100;

/**
 * Builds the form before anyone types: the client signs on the phone, and the name of the signer is suggested from the booking.
 * @param clientName Name of the client of the booking.
 * @returns The initial values.
 */
export function initialContractValues(clientName: string): ContractFormValues {
  return {
    kind: 'InPerson',
    signerName: clientName,
    idNumber: '',
    participantCount: '',
    sessionLocation: '',
    imageUse: null,
    accepted: false,
  };
}

/**
 * Keeps the digits of an identity number typed the way people write it, for example `1-1234-5678`.
 * @param typed What was typed.
 * @returns The digits when the text only has digits, hyphens and spaces; otherwise null.
 */
export function readIdDigits(typed: string): string | null {
  const stripped = typed.replace(/[-\s]/g, '');
  return /^\d+$/.test(stripped) ? stripped : null;
}

/**
 * Validates what the client declares when they sign on the phone, with the same rules as the backend.
 * @param values What was typed and chosen.
 * @returns The messages by field; empty when everything is valid.
 */
function validateDeclaration(values: ContractFormValues): { -readonly [Field in keyof ContractFormValues]?: string } {
  const errors: { -readonly [Field in keyof ContractFormValues]?: string } = {};

  const idDigits = readIdDigits(values.idNumber);
  if (idDigits === null || ![9, 11, 12].includes(idDigits.length)) {
    errors.idNumber = 'Escribe la cédula (9 dígitos) o el DIMEX (11 o 12 dígitos).';
  }

  const typedCount = values.participantCount.trim();
  const participants = /^\d+$/.test(typedCount) ? Number(typedCount) : Number.NaN;
  if (!Number.isInteger(participants) || participants < MIN_PARTICIPANTS || participants > MAX_PARTICIPANTS) {
    errors.participantCount = `Escribe cuántas personas vienen (de ${MIN_PARTICIPANTS} a ${MAX_PARTICIPANTS}).`;
  }

  if (values.sessionLocation.trim().length > MAX_LOCATION_LENGTH) {
    errors.sessionLocation = `El lugar no puede pasar de ${MAX_LOCATION_LENGTH} caracteres.`;
  }

  if (values.imageUse === null) {
    errors.imageUse = 'Elige si autoriza o no el uso de las fotos.';
  }

  if (!values.accepted) {
    errors.accepted = 'El cliente tiene que leer y aceptar el contrato.';
  }

  return errors;
}

/**
 * Validates the contract form and, when it is valid, builds the request. The backend enforces the same rules; checking them
 * here gives a message next to the field without a round trip. A contract on paper only needs a name; one signed on the phone
 * also needs what the client declares and that they accepted what they read.
 * @param values What was typed and chosen.
 * @param templateVersion Version of the contract the client read on the phone; it travels back so they sign what they read.
 * @returns The request, or the validation messages by field.
 */
export function validateContract(values: ContractFormValues, templateVersion: string): ContractFormResult {
  const name = values.signerName.trim();
  const errors: { -readonly [Field in keyof ContractFormValues]?: string } = {};

  if (name.length === 0) {
    errors.signerName = 'Escribe el nombre de quien firma.';
  } else if (name.length > MAX_SIGNER_NAME_LENGTH) {
    errors.signerName = `El nombre no puede pasar de ${MAX_SIGNER_NAME_LENGTH} caracteres.`;
  }

  if (values.kind === 'Paper') {
    const paperErrors: ContractFormErrors = errors;
    return Object.keys(paperErrors).length > 0
      ? { ok: false, errors: paperErrors }
      : { ok: true, request: { signerName: name, templateVersion: CONTRACT_TEMPLATE_VERSION, isPaperContract: true } };
  }

  const allErrors = { ...errors, ...validateDeclaration(values) };
  const idDigits = readIdDigits(values.idNumber);
  if (Object.keys(allErrors).length > 0 || idDigits === null || values.imageUse === null) {
    return { ok: false, errors: allErrors };
  }

  const location = values.sessionLocation.trim();
  return {
    ok: true,
    request: {
      signerName: name,
      templateVersion,
      isPaperContract: false,
      declaration: {
        idNumber: idDigits,
        participantCount: Number(values.participantCount.trim()),
        ...(location.length > 0 ? { sessionLocation: location } : {}),
        imageUse: values.imageUse,
      },
    },
  };
}
