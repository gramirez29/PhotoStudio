import { create } from 'zustand';
import type { MenuStore } from '../types/menu/menu.types';

/**
 * State of the side menu: the button of the header opens it and the menu closes itself when something is chosen. It lives in a
 * store because the button is in the header of the navigator and the menu is drawn above the whole app.
 */
export const useMenuStore = create<MenuStore>((set) => ({
  open: false,
  openMenu: () => set({ open: true }),
  closeMenu: () => set({ open: false }),
}));
