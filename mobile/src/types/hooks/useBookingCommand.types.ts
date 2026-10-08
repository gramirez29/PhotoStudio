/** An action on an existing booking, ready to be sent to the API. */
export type BookingCommand =
  | { readonly kind: 'cancel'; readonly reason: string | null }
  | { readonly kind: 'complete' }
  | { readonly kind: 'markClientAbsent' }
  | { readonly kind: 'revertClientAbsent'; readonly reason: string };
