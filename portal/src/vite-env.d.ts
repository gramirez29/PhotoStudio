/// <reference types="vite/client" />

/** Variables of the environment that Vite exposes to the page (all of them are public). */
interface ImportMetaEnv {
  /** Address of the PhotoStudio API. */
  readonly VITE_API_URL?: string;
}

/** Shape of `import.meta` with the variables above. */
interface ImportMeta {
  /** Variables of the environment. */
  readonly env: ImportMetaEnv;
}
