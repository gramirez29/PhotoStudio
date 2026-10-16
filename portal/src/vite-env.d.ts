/// <reference types="vite/client" />

/** Variables of the environment that Vite exposes to the page (all of them are public). */
interface ImportMetaEnv {
  /** Address of the PhotoStudio API. */
  readonly VITE_API_URL?: string;
  /** Name of the test environment this build is for (`develop`); missing in production. */
  readonly VITE_ENVIRONMENT?: string;
}

/** Shape of `import.meta` with the variables above. */
interface ImportMeta {
  /** Variables of the environment. */
  readonly env: ImportMetaEnv;
}
