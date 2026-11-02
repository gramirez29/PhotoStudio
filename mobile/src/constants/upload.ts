/**
 * What the phone does to prepare and upload the photos of a gallery. These values are the same specification as the web of the
 * photographer (`portal/src/constants/proof.ts` and `finals.ts`): a proof looks the same whichever device made it. Keep both in sync.
 */

/** Heaviest proof the API accepts, in bytes (1 MB). A proof that weighs more is rejected and deleted by the API. */
export const PROOF_MAX_BYTES = 1_048_576;

/** Longest side of a proof, in pixels. A proof is meant to be looked at, not printed or kept. */
export const PROOF_LONG_SIDE = 720;

/** JPEG qualities to try (0 to 100), from the one the proofs are meant to have to the lowest; the first that fits in the limit wins. */
export const PROOF_QUALITIES = [40, 30, 20] as const;

/** Factor applied to the sides of a proof that still weighs too much with the lowest quality. */
export const PROOF_SHRINK_FACTOR = 0.75;

/** Times the proof is shrunk before giving up; each time every quality is tried again. */
export const PROOF_MAX_SHRINKS = 3;

/** Width of the watermark as a fraction of the shorter side of the proof: big, in the middle, so cropping cannot remove it. */
export const WATERMARK_SIZE_RATIO = 0.7;

/** Opacity of the watermark: seen on any photo, without hiding the photo. */
export const WATERMARK_OPACITY = 0.6;

/** Blur of the shadow of the watermark, as a fraction of the shorter side, so a white mark is seen on a white photo too. */
export const WATERMARK_SHADOW_RATIO = 0.012;

/** Opacity of the shadow of the watermark. */
export const WATERMARK_SHADOW_OPACITY = 0.7;

/** Quality (0 to 1) of the intermediate image that is reduced first and then marked; the final proof is encoded from it once more. */
export const PROOF_INTERMEDIATE_QUALITY = 0.92;

/** Proofs prepared and uploaded together before they are registered. Memory is bounded: a batch is at most this many megabytes. */
export const PROOF_BATCH_SIZE = 10;

/** Proofs prepared at the same time inside a batch: the phone has less memory than a computer. */
export const PROOF_PREPARE_CONCURRENCY = 2;

/** Proofs sent to the storage at the same time inside a batch. */
export const PROOF_UPLOAD_CONCURRENCY = 3;

/** Longest name of a proof the API accepts, in characters. */
export const PROOF_NAME_MAX_LENGTH = 200;

/** Content type of every proof. The signed address of the storage only accepts this one. */
export const JPEG_CONTENT_TYPE = 'image/jpeg';

/** Heaviest final photo the API accepts, in bytes (50 MB). A final that weighs more is rejected and deleted by the API. */
export const FINAL_MAX_BYTES = 52_428_800;

/** Finals sent and registered together. Their places are asked right before sending, so the signed addresses never wait long. */
export const FINAL_BATCH_SIZE = 5;

/** Finals sent to the storage at the same time inside a batch. */
export const FINAL_UPLOAD_CONCURRENCY = 2;

/** Longest side of the thumbnail of a final, in pixels: big enough to look good in a grid, small enough to load fast. */
export const THUMB_LONG_SIDE = 720;

/** JPEG qualities (0 to 1) to try for a thumbnail, from the best to the lowest; the first that fits in the limit wins. */
export const THUMB_QUALITIES = [0.85, 0.75, 0.65, 0.55] as const;

/** Heaviest thumbnail the API accepts, in bytes (400 KB). */
export const THUMB_MAX_BYTES = 409_600;

/** Factor applied to the sides of a thumbnail that still weighs too much with the lowest quality. */
export const THUMB_SHRINK_FACTOR = 0.8;

/** Times a thumbnail is shrunk before giving up; each time every quality is tried again. */
export const THUMB_MAX_SHRINKS = 2;

/** Heaviest ZIP the API accepts, in bytes (500 MB). */
export const ZIP_MAX_BYTES = 524_288_000;

/** What the document picker offers for the ZIP. */
export const ZIP_MIME_TYPES = ['application/zip', 'application/x-zip-compressed'] as const;

/** Offset from UTC, in minutes, assumed for the date a camera writes without a time zone: Costa Rica, which has no daylight saving time. */
export const CAMERA_UTC_OFFSET_MINUTES = -360;

/** Folder, inside the cache of the app, where the photos prepared for upload wait until they are sent. */
export const STAGING_FOLDER = 'uploads';
