import type {
  AuthSessionResponse,
  ChangeEmailRequest,
  ChangePasswordRequest,
  EmailVerificationSentResponse,
  VerifyEmailRequest,
} from './auth';

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

  /**
   * Proves the email with the code that was sent there. Verifying changes what the token can do, so the answer is a new session.
   * @param request The code the user typed.
   * @param signal Optional signal to cancel the request.
   * @returns The new session, whose token lets the user into the app.
   */
  verifyEmail(request: VerifyEmailRequest, signal?: AbortSignal): Promise<AuthSessionResponse>;

  /**
   * Asks for a new code to verify the email. The code goes to the address of the account, which the user never types.
   * @param signal Optional signal to cancel the request.
   * @returns Where the code went (masked) and when it expires.
   */
  resendEmailVerification(signal?: AbortSignal): Promise<EmailVerificationSentResponse>;

  /**
   * Replaces the email of an account that has not verified it, for the user who misspelled it, and sends the code to the new
   * address. The backend refuses it once the email is verified and keeps the limits of the code.
   * @param request The address the user wants.
   * @param signal Optional signal to cancel the request.
   * @returns Where the code went (masked) and when it expires.
   */
  changeUnverifiedEmail(request: ChangeEmailRequest, signal?: AbortSignal): Promise<EmailVerificationSentResponse>;
}
