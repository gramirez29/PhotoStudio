import type { ProofUploadSlot } from '../types/api/proofs';
import { NetworkError } from './httpClient';

/** The storage refused a proof: the status it answered with. */
export class ProofUploadError extends Error {
  /** HTTP status the storage answered with. */
  public readonly status: number;

  /**
   * Creates the error. The address is not part of the message: it is a secret that expires, and it must not reach any log.
   * @param status HTTP status the storage answered with.
   */
  public constructor(status: number) {
    super(`The storage refused the proof (HTTP ${status}).`);
    this.name = 'ProofUploadError';
    this.status = status;
  }
}

/**
 * Sends a proof straight to the storage, to the signed address the API gave. The request carries no cookies and no referrer: the
 * storage is not the API, and the address is all it needs.
 * @param slot The place the API gave for this proof.
 * @param proof The proof to send.
 * @param signal Optional signal to cancel the upload.
 * @param fetchFn Function that sends the request; defaults to the global `fetch`. Tests replace it.
 * @returns A promise that resolves when the storage has the proof.
 * @throws NetworkError When the storage could not be reached.
 * @throws ProofUploadError When the storage answered with an error.
 */
export async function putProof(slot: ProofUploadSlot, proof: Blob, signal?: AbortSignal, fetchFn: typeof fetch = (input, init) => fetch(input, init)): Promise<void> {
  let response: Response;
  try {
    response = await fetchFn(slot.url, {
      method: 'PUT',
      headers: { ...slot.headers },
      body: proof,
      credentials: 'omit',
      referrerPolicy: 'no-referrer',
      signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    throw new NetworkError();
  }

  if (!response.ok) {
    throw new ProofUploadError(response.status);
  }
}
