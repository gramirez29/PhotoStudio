import { studioApi } from '../api/client';
import { putProof } from '../api/proofStorage';
import type { FinalUploader } from '../types/media/finalUploader.types';
import { createFinalUploader } from './finalUploader';
import { readJpegInfo } from './readJpegInfo';

/** The uploader of finals, wired to the real API and the real storage. */
export const browserFinalUploader: FinalUploader = createFinalUploader({
  api: studioApi,
  put: (slot, file, signal) => putProof(slot, file, signal),
  readInfo: readJpegInfo,
});
