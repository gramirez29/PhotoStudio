import { studioApi } from '../api/client';
import { putProof } from '../api/proofStorage';
import type { ProofUploader } from '../types/media/proofUploader.types';
import { browserProofRenderer } from './browserProofRenderer';
import { createProofUploader } from './proofUploader';

/** The uploader the pages use: it prepares the proofs in this browser, sends them to the storage and registers them with the API. */
export const browserProofUploader: ProofUploader = createProofUploader({
  api: studioApi,
  renderer: browserProofRenderer,
  put: (slot, proof, signal) => putProof(slot, proof, signal),
});
