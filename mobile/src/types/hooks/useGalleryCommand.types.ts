import type { EditGalleryTermsRequest } from '../api/gallery';

/** Something the photographer asks of a gallery. The gallery it belongs to is given to the hook. */
export type GalleryCommand =
  | { readonly kind: 'editTerms'; readonly terms: EditGalleryTermsRequest }
  | { readonly kind: 'removeProof'; readonly photoId: string }
  | { readonly kind: 'openSelection' }
  | { readonly kind: 'reopenSelection'; readonly reason: string }
  | { readonly kind: 'publishFinals' }
  | { readonly kind: 'releaseExtras' }
  | { readonly kind: 'removeZip' }
  | { readonly kind: 'release' };
