import type { ReactNode } from 'react';

/** Props of the `KeyboardAwareScreen` component. */
export interface KeyboardAwareScreenProps {
  /** Content of the screen, usually a form. */
  readonly children: ReactNode;
  /**
   * Where the content sits when it is shorter than the screen: `center` for short forms such as the sign-in (the default), `top`
   * for forms that read from the top down, such as creating a booking.
   */
  readonly align?: KeyboardAwareAlignment;
}

/** Vertical placement of the content of a `KeyboardAwareScreen` when it does not fill the screen. */
export type KeyboardAwareAlignment = 'center' | 'top';
