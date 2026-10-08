import type { AuthSessionResponse, RegisterRequest } from './auth';

/** Authentication endpoints. They need no access token, so they use a client without session handling. */
export interface AuthApi {
  /**
   * Creates an account and signs it in.
   * @param request Data of the new user.
   * @param signal Optional signal to cancel the request.
   * @returns The new session.
   */
  register(request: RegisterRequest, signal?: AbortSignal): Promise<AuthSessionResponse>;

  /**
   * Signs in with username and password.
   * @param username Username of the user.
   * @param password Password of the user.
   * @param signal Optional signal to cancel the request.
   * @returns The new session.
   */
  login(username: string, password: string, signal?: AbortSignal): Promise<AuthSessionResponse>;

  /**
   * Exchanges a refresh token for a new session. The refresh token cannot be used again.
   * @param refreshToken Refresh token held by the app.
   * @param signal Optional signal to cancel the request.
   * @returns The new session, with the replacement refresh token.
   */
  refresh(refreshToken: string, signal?: AbortSignal): Promise<AuthSessionResponse>;

  /**
   * Closes the session that owns the refresh token. The backend answers the same whether or not the token existed.
   * @param refreshToken Refresh token of the session to close.
   * @param signal Optional signal to cancel the request.
   * @returns A promise that resolves when the backend answered.
   */
  logout(refreshToken: string, signal?: AbortSignal): Promise<void>;
}
