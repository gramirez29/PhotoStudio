import type { PUSH_PLATFORMS } from '../../constants/push';

/** Operating system of the phone that receives push notifications. */
export type PushPlatform = (typeof PUSH_PLATFORMS)[number];

/** Body of `POST /api/push-devices`: the phone that should receive push notifications. */
export interface RegisterPushDeviceRequest {
  /** Expo push token of the phone, for example `ExponentPushToken[...]`. */
  readonly token: string;
  /** Operating system of the phone. */
  readonly platform: PushPlatform;
}

/** Body of `POST /api/push-devices/unregister`: the phone that should stop receiving push notifications. */
export interface UnregisterPushDeviceRequest {
  /** Expo push token of the phone. */
  readonly token: string;
}
