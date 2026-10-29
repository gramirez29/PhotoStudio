import { create } from 'zustand';
import { DEFAULT_THEME_PREFERENCE } from '../constants/appearance';
import type { AppearanceStore } from '../types/appearance/appearance.types';

/**
 * State of the appearance: what the photographer chose for the theme. It starts with the default and the appearance manager
 * replaces it with what was saved on the device and with each new choice.
 */
export const useAppearanceStore = create<AppearanceStore>((set) => ({
  preference: DEFAULT_THEME_PREFERENCE,
  setPreference: (preference) => set({ preference }),
}));
