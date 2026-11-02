/** Heaviest final photo the API accepts, in bytes (50 MB). A final that weighs more is rejected and deleted by the API. */
export const FINAL_MAX_BYTES = 52_428_800;

/** Finals sent and registered together. Their places are asked right before sending, so the signed addresses never wait long. */
export const FINAL_BATCH_SIZE = 5;

/** Finals sent to the storage at the same time inside a batch. */
export const FINAL_UPLOAD_CONCURRENCY = 3;

/** What the file picker offers for finals: JPEG files only. */
export const FINAL_ACCEPT = 'image/jpeg,.jpg,.jpeg';

/** Longest side of the thumbnail of a final, in pixels: big enough to look good in a grid, small enough to load fast. */
export const THUMB_LONG_SIDE = 720;

/** JPEG qualities to try for a thumbnail, from the best to the lowest; the first that fits in the limit wins. */
export const THUMB_QUALITIES = [0.85, 0.75, 0.65, 0.55] as const;

/** Heaviest thumbnail the API accepts, in bytes (400 KB). */
export const THUMB_MAX_BYTES = 409_600;

/** Factor applied to the sides of a thumbnail that still weighs too much with the lowest quality. */
export const THUMB_SHRINK_FACTOR = 0.8;

/** Times a thumbnail is shrunk before giving up; each time every quality is tried again. */
export const THUMB_MAX_SHRINKS = 2;

/** Heaviest ZIP the API accepts, in bytes (500 MB). */
export const ZIP_MAX_BYTES = 524_288_000;

/** What the file picker offers for the ZIP. */
export const ZIP_ACCEPT = '.zip,application/zip,application/x-zip-compressed';

/** How much of the start of a final is read to find its size in pixels and the date it was taken: the metadata is at the start. */
export const JPEG_INFO_READ_BYTES = 1_048_576;

/** Offset from UTC, in minutes, assumed for the date a camera writes without a time zone: Costa Rica, which has no daylight saving time. */
export const CAMERA_UTC_OFFSET_MINUTES = -360;

/** Pause between one download and the next when the client downloads many photos, so the browser does not drop any. */
export const DOWNLOAD_PAUSE_MS = 700;
