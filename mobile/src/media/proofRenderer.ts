import { PROOF_LONG_SIDE, PROOF_MAX_BYTES, PROOF_MAX_SHRINKS, PROOF_QUALITIES, PROOF_SHRINK_FACTOR } from '../constants/upload';
import type { ProofEngine, ProofRenderer, ProofRenderFailure, RenderedProof } from '../types/media/upload.types';
import { fitWithin } from '../utils/proofImage';

/** A photo could not be turned into a proof; `kind` says why, so the screen can say it in words. */
export class ProofRenderError extends Error {
  /** Why the photo failed. */
  public readonly kind: ProofRenderFailure;

  /**
   * Creates the error.
   * @param kind Why the photo failed.
   */
  public constructor(kind: ProofRenderFailure) {
    super(`The photo could not be turned into a proof (${kind}).`);
    this.name = 'ProofRenderError';
    this.kind = kind;
  }
}

/**
 * Creates the renderer of proofs. A proof is the photo reduced to a small size, encoded as a JPEG of low quality and with the logo of
 * the app across the middle. It is made on the phone: the full photo never leaves it, and the server never has to do the work. If the
 * proof still weighs more than the limit with the lowest quality, it is made smaller. The same specification as the web of the
 * photographer, so a proof looks the same whichever device made it.
 * @param engine What the renderer needs from the device.
 * @returns The renderer.
 */
export function createProofRenderer(engine: ProofEngine): ProofRenderer {
  return {
    async render(uri: string): Promise<RenderedProof> {
      let source;
      try {
        source = await engine.open(uri);
      } catch {
        throw new ProofRenderError('decode');
      }

      try {
        let longSide = PROOF_LONG_SIDE;
        for (let shrink = 0; shrink <= PROOF_MAX_SHRINKS; shrink++) {
          const size = fitWithin(source, longSide);
          for (const quality of PROOF_QUALITIES) {
            const bytes = await source.encode(size, quality);
            if (bytes === null) {
              throw new ProofRenderError('encode');
            }

            if (bytes.byteLength <= PROOF_MAX_BYTES) {
              return { bytes, size, quality };
            }
          }

          longSide = Math.round(longSide * PROOF_SHRINK_FACTOR);
        }

        throw new ProofRenderError('too_big');
      } catch (error) {
        throw error instanceof ProofRenderError ? error : new ProofRenderError('encode');
      } finally {
        source.close();
      }
    },
  };
}
