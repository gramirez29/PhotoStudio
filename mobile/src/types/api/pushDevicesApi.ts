import type { RegisterPushDeviceRequest, UnregisterPushDeviceRequest } from './pushDevices';

/** Push device endpoints of the signed-in photographer. They need the access token, so they use the client that manages the session. */
export interface PushDevicesApi {
  /**
   * Registers the phone. Registering the same phone again is harmless; a phone registered by another photographer moves to this one.
   * @param request Phone to register.
   * @param signal Optional signal to cancel the request.
   */
  register(request: RegisterPushDeviceRequest, signal?: AbortSignal): Promise<void>;

  /**
   * Stops sending push notifications to the phone. A token that is unknown, or belongs to someone else, is ignored.
   * @param request Phone to forget.
   * @param signal Optional signal to cancel the request.
   */
  unregister(request: UnregisterPushDeviceRequest, signal?: AbortSignal): Promise<void>;
}
