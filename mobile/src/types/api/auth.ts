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
  /** Username of the user. */
  readonly username: string;
  /** Email of the user; empty for users created before the email existed. */
  readonly email: string;
  /** Display name of the user. */
  readonly name: string;
}

/** Body to create an account (backend `RegisterRequest`). */
export interface RegisterRequest {
  /** Login name: 3 to 30 letters, digits, dots, hyphens or underscores. */
  readonly username: string;
  /** Email address. */
  readonly email: string;
  /** Password: 8 to 128 characters. */
  readonly password: string;
  /** Display name. */
  readonly name: string;
  /** Phone number, in international format. */
  readonly phone: string;
}
