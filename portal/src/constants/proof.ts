/** Heaviest proof the API accepts, in bytes (1 MB). A proof that weighs more is rejected and deleted by the API. */
export const PROOF_MAX_BYTES = 1_048_576;

/** Longest side of a proof, in pixels. A proof is meant to be looked at, not printed or kept. */
export const PROOF_LONG_SIDE = 720;

/** JPEG qualities to try, from the one the proofs are meant to have to the lowest; the first that fits in the limit wins. */
export const PROOF_QUALITIES = [0.4, 0.3, 0.2] as const;

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

/** Proofs prepared and uploaded together before they are registered. Memory is bounded: a batch is at most this many megabytes. */
export const PROOF_BATCH_SIZE = 10;

/** Proofs prepared at the same time inside a batch. */
export const PROOF_PREPARE_CONCURRENCY = 3;

/** Proofs sent to the storage at the same time inside a batch. */
export const PROOF_UPLOAD_CONCURRENCY = 4;

/** Longest name of a proof the API accepts, in characters. */
export const PROOF_NAME_MAX_LENGTH = 200;

/** Content type of every proof. The signed address of the storage only accepts this one. */
export const PROOF_CONTENT_TYPE = 'image/jpeg';

/** What the file picker offers: JPEG files only. */
export const PROOF_ACCEPT = 'image/jpeg,.jpg,.jpeg';

/** Minutes after which the signed addresses to look at the proofs stop working; the list is asked again a little before. */
export const PROOF_LIST_STALE_MS = 4 * 60 * 1000;
