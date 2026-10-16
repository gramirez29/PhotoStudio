/** The page an address shows. */
export type Route =
  | { readonly kind: 'home' }
  | { readonly kind: 'booking'; readonly token: string }
  | { readonly kind: 'notFound' };
