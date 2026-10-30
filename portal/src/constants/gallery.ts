/** States of a gallery, as the backend names them. */
export const GALLERY_STATUSES = ['Pending', 'InSelection', 'InEditing', 'Ready', 'Delivered', 'Archived'] as const;

/** What the photographer can do with a gallery, as the backend names the actions. */
export const GALLERY_ACTIONS = ['EditTerms', 'UploadProofs', 'RemoveProof', 'OpenSelection'] as const;

/** Galleries in a page of the list; the page asks the backend for exactly this many. */
export const GALLERIES_PAGE_SIZE = 5;
