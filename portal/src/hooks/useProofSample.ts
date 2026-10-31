import { useEffect, useState } from 'react';
import { browserProofRenderer } from '../media/browserProofRenderer';
import type { ProofSample } from '../types/hooks/useProofSample.types';

/**
 * Makes one proof from a photo so the photographer can see how the proofs of their client will look before everything is
 * uploaded. The sample stays in the browser: it is shown from a temporary address that is released when it is not needed.
 * @param file The photo to make the sample from. The component that uses it is given a new key for each photo, so a sample is never reused for another one.
 * @returns Whether the sample is being made, is ready (with the address to show it) or could not be made.
 */
export function useProofSample(file: File | undefined): ProofSample {
  const [sample, setSample] = useState<ProofSample>({ state: 'loading', url: null });

  useEffect(() => {
    if (file === undefined) {
      return undefined;
    }

    let cancelled = false;
    let address: string | null = null;
    browserProofRenderer
      .render(file)
      .then((proof) => {
        if (cancelled) {
          return;
        }

        address = URL.createObjectURL(proof.blob);
        setSample({ state: 'ready', url: address });
      })
      .catch(() => {
        if (!cancelled) {
          setSample({ state: 'failed', url: null });
        }
      });

    return () => {
      cancelled = true;
      if (address !== null) {
        URL.revokeObjectURL(address);
      }
    };
  }, [file]);

  return sample;
}
