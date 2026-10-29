import type { Channel, ClientDeclarationResponse, ContractResponse, ImageUse } from '../types/api/booking';
import type { ContractLayout } from '../types/utils/contract.types';
import { formatDateTime } from './format';

/** How each channel of a signature reads after "Firmado por ...". */
const CHANNEL_LABELS: Readonly<Record<Channel, string>> = {
  Portal: 'desde el enlace',
  PhotographerApp: 'desde la app',
  InPerson: 'en persona',
  External: 'en papel',
};

/** What the client allowed to do with the photos, as the photographer reads it. */
const IMAGE_USE_LABELS: Readonly<Record<ImageUse, string>> = {
  Authorized: 'Autoriza publicar las fotos',
  NotAuthorized: 'No autoriza publicar las fotos',
};

/**
 * Writes an identity number the way it is usually read: a cédula of 9 digits as `1-1234-5678`. A DIMEX, or anything else, is
 * left as it was saved.
 * @param digits Identity number as saved, digits only.
 * @returns The text to show.
 */
export function formatIdNumber(digits: string): string {
  return /^\d{9}$/.test(digits) ? `${digits.slice(0, 1)}-${digits.slice(1, 5)}-${digits.slice(5)}` : digits;
}

/**
 * Describes what the client declared when signing through the portal: who they are, how many come, where, and what the photos
 * may be used for.
 * @param declaration What the client declared.
 * @returns The lines, in Spanish.
 */
export function describeDeclaration(declaration: ClientDeclarationResponse): readonly string[] {
  return [
    `Cédula o DIMEX: ${formatIdNumber(declaration.idNumber)}`,
    `Personas en la sesión: ${declaration.participantCount}`,
    `Lugar: ${declaration.sessionLocation ?? 'por definir con el cliente'}`,
    `Uso de las fotos: ${IMAGE_USE_LABELS[declaration.imageUse]}`,
  ];
}

/**
 * Describes the contract of a booking: whether it is signed, by whom, how and when, and what the client declared when they
 * signed through the portal.
 * @param contract Contract of the booking, or null when it is not signed.
 * @returns The lines, in Spanish.
 */
export function describeContract(contract: ContractResponse | null): readonly string[] {
  if (contract === null) {
    return ['Pendiente de firma'];
  }

  const signature = `Firmado por ${contract.signerName} ${CHANNEL_LABELS[contract.channel]} el ${formatDateTime(contract.signedAt)}`;
  return contract.declaration === null ? [signature] : [signature, ...describeDeclaration(contract.declaration)];
}

/** How each image use reads when the client chooses it on the phone (clause 12 of the contract). */
export const IMAGE_USE_CHOICE_LABELS: Readonly<Record<ImageUse, string>> = {
  Authorized: 'Autorizo publicar las fotos',
  NotAuthorized: 'No autorizo publicar las fotos',
};

/**
 * Finds the image use that a label of the list stands for.
 * @param label Label the user picked.
 * @returns The image use, or null when the label is not one of the choices.
 */
export function imageUseFromChoice(label: string): ImageUse | null {
  return (Object.keys(IMAGE_USE_CHOICE_LABELS) as ImageUse[]).find((use) => IMAGE_USE_CHOICE_LABELS[use] === label) ?? null;
}

/**
 * Splits the text of the contract into what the screen draws: the title, then one paragraph per non-blank line. A line that starts
 * with spaces (the options of the image use) is marked as indented.
 * @param text Text of the contract, one paragraph per line.
 * @returns The title and the paragraphs.
 */
export function splitContractText(text: string): ContractLayout {
  const lines = text.split('\n').filter((line) => line.trim().length > 0);
  const [title, ...body] = lines;
  return {
    title: title === undefined ? null : title.trim(),
    paragraphs: body.map((line) => ({ text: line.trim(), indented: /^\s/.test(line) })),
  };
}
