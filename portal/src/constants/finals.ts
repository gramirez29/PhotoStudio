/** Heaviest final photo the API accepts, in bytes (50 MB). A final that weighs more is rejected and deleted by the API. */
export const FINAL_MAX_BYTES = 52_428_800;

/** Finals sent and registered together. Their places are asked right before sending, so the signed addresses never wait long. */
export const FINAL_BATCH_SIZE = 5;

/** Finals sent to the storage at the same time inside a batch. */
export const FINAL_UPLOAD_CONCURRENCY = 3;

/** What the file picker offers for finals: JPEG files only. */
export const FINAL_ACCEPT = 'image/jpeg,.jpg,.jpeg';

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
