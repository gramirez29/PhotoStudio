import { existsSync } from 'node:fs';
import process from 'node:process';
import type { ConfigContext, ExpoConfig } from 'expo/config';

/** Path of the Firebase Android config used when no EAS file variable provides one. */
const LOCAL_GOOGLE_SERVICES_FILE = './google-services.json';

/**
 * Finds the Firebase config (`google-services.json`) that Android needs for push notifications. EAS builds provide it as the
 * file variable `GOOGLE_SERVICES_JSON` (the file is git-ignored, so it is never uploaded with the source); locally it is read
 * from the project folder. Without either, nothing is returned so that `expo start` and the tests work before Firebase exists.
 * @returns The path of the file, or undefined when there is none.
 */
function resolveGoogleServicesFile(): string | undefined {
  const fromEas = process.env.GOOGLE_SERVICES_JSON;
  if (fromEas !== undefined && fromEas.length > 0) {
    return fromEas;
  }

  return existsSync(LOCAL_GOOGLE_SERVICES_FILE) ? LOCAL_GOOGLE_SERVICES_FILE : undefined;
}

/**
 * Completes the static configuration of `app.json` with the values that depend on the environment.
 * @param context Expo context, with the static configuration read from `app.json`.
 * @returns The final Expo configuration.
 */
export default ({ config }: ConfigContext): ExpoConfig => {
  const googleServicesFile = resolveGoogleServicesFile();

  return {
    ...config,
    name: config.name ?? 'PhotoSense Studio',
    slug: config.slug ?? 'photostudio',
    android: {
      ...config.android,
      ...(googleServicesFile === undefined ? {} : { googleServicesFile }),
    },
  };
};
