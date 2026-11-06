/** A route of the stack, as much of it as moving the home screen needs. */
export interface StackRoute {
  /** Name of the route. */
  readonly name: string;
  /** Key that identifies the route while it is in the stack; a route that is kept keeps its screen as it is. */
  readonly key?: string;
}

/** The part of a screen's navigation object that moving the home screen needs, so the code that does it can be tested with a stand-in. */
export interface ReplaceableNavigation {
  /** Reads the state of the navigator that holds the screen. */
  readonly getState: () => { readonly routes: readonly StackRoute[] } | undefined;
  /** Replaces the whole stack. */
  readonly reset: (state: { readonly index: number; readonly routes: readonly StackRoute[] }) => void;
}
