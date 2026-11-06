/** The part of a screen's navigation object that moving a screen needs, so the code that does it can be tested with a stand-in. */
export interface ReplaceableNavigation {
  /** Sends an action to the navigator on behalf of the screen. */
  readonly dispatch: (action: { readonly type: string; readonly payload?: { readonly name: string } }) => void;
  /** Reads the state of the navigator that holds the screen. */
  readonly getState: () => { readonly routes: readonly { readonly name: string }[] } | undefined;
}
