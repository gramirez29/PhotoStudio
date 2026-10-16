/**
 * Values of the push notifications feature. The platform values mirror the backend `PushPlatform` enum and validate nothing at
 * runtime: they define the type of the request that registers the phone.
 */

/** Every value of the backend `PushPlatform` enum. */
export const PUSH_PLATFORMS = ['Android', 'Ios'] as const;

/** Identifier of the Android notification channel the backend sends to (`channelId` of every push); the app must create it. */
export const ANDROID_NOTIFICATION_CHANNEL_ID = 'default';

/** Name of the channel, as Android shows it in the notification settings of the app. */
export const ANDROID_NOTIFICATION_CHANNEL_NAME = 'Avisos';

/** How long signing out waits for the backend to forget the phone, in milliseconds. Signing out must work without a connection. */
export const PUSH_UNREGISTER_TIMEOUT_MS = 3000;
