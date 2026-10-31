/** The page an address shows. */
export type Route =
  | { readonly kind: 'home' }
  | { readonly kind: 'studio' }
  | { readonly kind: 'studioGallery'; readonly galleryId: string }
  | { readonly kind: 'booking'; readonly token: string }
  | { readonly kind: 'notFound' };
