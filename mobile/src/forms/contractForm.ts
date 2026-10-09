import { CONTRACT_TEMPLATE_VERSION } from '../constants/contract';
import type { ContractFormResult, ContractSignatureKind } from '../types/forms/contractForm.types';

/** Longest signer name the form accepts. */
export const MAX_SIGNER_NAME_LENGTH = 100;

/**
 * Validates the contract form and builds the request.
 * @param signerName Name of the person who signs, as typed.
 * @param kind Whether the client signs on the phone or the contract is a paper one.
 * @returns The request to send, or the message to show.
 */
export function validateContract(signerName: string, kind: ContractSignatureKind): ContractFormResult {
  const name = signerName.trim();

  if (name.length === 0) {
    return { ok: false, error: 'Escribe el nombre de quien firma.' };
  }

  if (name.length > MAX_SIGNER_NAME_LENGTH) {
    return { ok: false, error: `El nombre no puede pasar de ${MAX_SIGNER_NAME_LENGTH} caracteres.` };
  }

  return {
    ok: true,
    request: { signerName: name, templateVersion: CONTRACT_TEMPLATE_VERSION, isPaperContract: kind === 'Paper' },
  };
}
