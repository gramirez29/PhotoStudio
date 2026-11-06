import { create } from 'zustand';
import { DEFAULT_HOME_SCREEN } from '../constants/home';
import type { HomeStore } from '../types/home/home.types';

/** State of the home screen choice. It starts with the default; the home manager replaces it with what was saved on the device. */
export const useHomeStore = create<HomeStore>((set) => ({
  screen: DEFAULT_HOME_SCREEN,
  loaded: false,
  redirected: false,
  setScreen: (screen) => set({ screen }),
  markLoaded: () => set({ loaded: true }),
  markRedirected: () => set({ redirected: true }),
}));
