import { studioApi } from '../api/client';
import { putProof } from '../api/proofStorage';
import type { FinalUploader } from '../types/media/finalUploader.types';
import { browserThumbRenderer } from './browserThumbRenderer';
import { createFinalUploader } from './finalUploader';
import { readJpegInfo } from './readJpegInfo';

/** The uploader of finals, wired to the real API and the real storage. */
export const browserFinalUploader: FinalUploader = createFinalUploader({
  api: studioApi,
  put: (slot, file, signal) => putProof(slot, file, signal),
  readInfo: readJpegInfo,
  renderThumb: (file) => browserThumbRenderer.render(file),
});
