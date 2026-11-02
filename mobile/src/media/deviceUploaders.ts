import { galleryApi } from '../api/client';
import { createFileStaging, discardCachedFile } from './fileStaging';
import { createFinalUploader } from './finalUploader';
import { createManipulatorThumbEngine } from './manipulatorThumbEngine';
import { createProofRenderer } from './proofRenderer';
import { createProofUploader } from './proofUploader';
import { createSkiaProofEngine } from './skiaProofEngine';
import { createStorageUploader } from './storageUpload';
import { createThumbRenderer } from './thumbRenderer';
import { createZipUploader } from './zipUploader';

/** The logo that goes across the middle of every proof: the same image as the web of the photographer. */
const WATERMARK_IMAGE = require('../../assets/watermark.png') as number;

/** Keeps the files the app prepares until they are sent. */
const staging = createFileStaging();

/** Sends files from the phone straight to the storage. */
const storage = createStorageUploader();

/** The renderer of proofs, drawn with Skia. */
export const proofRenderer = createProofRenderer(createSkiaProofEngine(WATERMARK_IMAGE, discardCachedFile));

/**
 * Makes the sample the photographer confirms before the proofs go up: the proof of one photo, as the client will see it, kept as a
 * file of the cache so it can be shown. The caller deletes it with `discardPickedFile` when it is no longer shown.
 * @param uri Address of the photo on the phone.
 * @returns Address of the sample proof.
 */
export async function makeProofSample(uri: string): Promise<string> {
  return staging.stage((await proofRenderer.render(uri)).bytes).uri;
}

/** The uploader of proofs, wired to the real API, the device and the storage. */
export const deviceProofUploader = createProofUploader({ api: galleryApi, renderer: proofRenderer, staging, storage });

/** The uploader of finals, wired to the real API, the device and the storage. */
export const deviceFinalUploader = createFinalUploader({ api: galleryApi, thumbs: createThumbRenderer(createManipulatorThumbEngine(discardCachedFile)), staging, storage });

/** The uploader of the ZIP, wired to the real API and the storage. */
export const deviceZipUploader = createZipUploader({ api: galleryApi, storage });

/** Deletes a file of the cache of the app, such as the copy a picker made of a photo. It never touches anything outside the cache. */
export const discardPickedFile: (uri: string) => void = discardCachedFile;
