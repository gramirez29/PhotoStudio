/**
 * Values of the backend gallery enums, kept as `as const` arrays like the booking enums: they validate responses at runtime and
 * define the matching types in `src/types/api/gallery.ts`. Keep in sync with the backend.
 */

/** Every value of the backend `GalleryStatus` enum. */
export const GALLERY_STATUSES = ['Pending', 'InSelection', 'InEditing', 'Ready', 'Delivered', 'Archived'] as const;

/** Every value of the backend `GalleryAction` enum: what the photographer can do with a gallery. */
export const GALLERY_ACTIONS = [
  'EditTerms',
  'UploadProofs',
  'RemoveProof',
  'OpenSelection',
  'ReopenSelection',
  'UploadFinals',
  'PublishFinals',
  'ReleaseExtras',
  'Release',
  'UploadZip',
  'RemoveZip',
] as const;

/** Longest reason the backend accepts when the selection is reopened. */
export const GALLERY_REASON_MAX_LENGTH = 500;

/** Most photos a gallery holds, counting the included and the gift ones. */
export const GALLERY_MAX_PHOTOS = 150;
