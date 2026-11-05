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
  /** Whether the user may run system-wide work. Only decides what the app shows; the backend enforces it with the token. */
  readonly isSystemOperator: boolean;
  /** Whether the user may use the app: they proved their email, or verification is not required for them. While it is false the app only shows the screen to verify the email. */
  readonly emailVerified: boolean;
}

/** Body to prove the email with the code that was sent there (backend `VerifyEmailRequest`). */
export interface VerifyEmailRequest {
  /** The six digits the user typed. */
  readonly code: string;
}

/** Body to replace the email of an account that has not verified it (backend `ChangeEmailRequest`). */
export interface ChangeEmailRequest {
  /** The address the user wants, already normalized. */
  readonly email: string;
}

/** Confirmation that a code to verify the email was sent (backend `EmailVerificationSentResponse`). */
export interface EmailVerificationSentResponse {
  /** Address the code went to, partly hidden, for example `a***@gmail.com`. */
  readonly maskedEmail: string;
  /** Instant the code stops being accepted (ISO 8601). */
  readonly expiresAt: string;
}

/** Body to ask for the code that lets a user who forgot their password choose a new one (backend `PasswordResetRequest`). */
export interface PasswordResetRequest {
  /** Username or email of the account. */
  readonly identifier: string;
}

/** Body to choose a new password with the code that was sent to the email (backend `PasswordResetConfirmRequest`). */
export interface PasswordResetConfirmRequest {
  /** Username or email of the account. */
  readonly identifier: string;
  /** The six digits of the code. */
  readonly code: string;
  /** Password the user wants: 8 to 128 characters. */
  readonly newPassword: string;
}

/** Body to change the password (backend `ChangePasswordRequest`). */
export interface ChangePasswordRequest {
  /** Password the user has now. */
  readonly currentPassword: string;
  /** Password the user wants: 8 to 128 characters. */
  readonly newPassword: string;
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
