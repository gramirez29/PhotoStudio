import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

/** Vite and Vitest configuration of the portal. */
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
  build: { sourcemap: false },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
