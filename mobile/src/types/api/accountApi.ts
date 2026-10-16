import type { AuthSessionResponse, ChangePasswordRequest } from './auth';

/** Account endpoints of the signed-in user. They need the access token, so they use the client that manages the session. */
export interface AccountApi {
  /**
   * Changes the password. The backend closes the other sessions of the user and returns the session of this device, which
   * the caller must adopt: the refresh token the device had is no longer valid.
   * @param request Current and new password.
   * @param signal Optional signal to cancel the request.
   * @returns The new session of this device.
   */
  changePassword(request: ChangePasswordRequest, signal?: AbortSignal): Promise<AuthSessionResponse>;
}
