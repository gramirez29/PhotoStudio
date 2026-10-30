/** State of the side menu of the app: whether it is open. It holds no side effects. */
export interface MenuStore {
  /** Whether the menu is open. */
  readonly open: boolean;
  /** Opens the menu. */
  readonly openMenu: () => void;
  /** Closes the menu. */
  readonly closeMenu: () => void;
}
