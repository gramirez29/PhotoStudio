/**
 * Writes what a screen reader says for the row of proofs of payment waiting for review.
 * @param count How many bookings have one.
 * @returns For example "Comprobantes por verificar (2)".
 */
export function pendingProofsLabel(count: number): string {
  return `Comprobantes por verificar (${count})`;
}

/**
 * Writes what a screen reader says for the row of tentative bookings about to expire.
 * @param count How many there are.
 * @returns For example "Reservas tentativas por vencer en 24 horas (2)".
 */
export function expiringSoonLabel(count: number): string {
  return `Reservas tentativas por vencer en 24 horas (${count})`;
}

/**
 * Writes what a screen reader says for the row of delivered galleries whose space can be freed.
 * @param count How many there are.
 * @returns For example "Galerías por archivar (2)".
 */
export function archivableLabel(count: number): string {
  return `Galerías por archivar (${count})`;
}
