/** A signed-in session as the backend returns it (`POST /api/auth/login` and `/api/auth/refresh`). */
export interface AuthSessionResponse {
  /** Short-lived JWT sent as `Authorization: Bearer`. */
  readonly accessToken: string;
  /** Instant the access token stops being accepted (ISO 8601). */
  readonly accessTokenExpiresAt: string;
  /** Secret that obtains a new session. Single-use: the response of a refresh carries its replacement. */
  readonly refreshToken: string;
  /** Instant the refresh token stops being valid (ISO 8601). */
  readonly refreshTokenExpiresAt: string;
  /** Photographer (tenant) identifier (GUID). */
  readonly photographerId: string;
  /** Photographer email. */
  readonly email: string;
}
