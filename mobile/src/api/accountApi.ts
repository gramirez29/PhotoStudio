import type { AccountApi } from '../types/api/accountApi';
import type { HttpClient } from '../types/api/http';
import type { EmailVerificationSentResponse } from '../types/api/auth';
import { parseAuthSession } from './authApi';
import { expectRecord, readString } from './guards';

/**
 * Parses the confirmation that a code to verify the email was sent.
 * @param value Unknown response body.
 * @returns The typed confirmation.
 */
export function parseEmailVerificationSent(value: unknown): EmailVerificationSentResponse {
  const path = 'emailVerificationSent';
  const record = expectRecord(value, path);
  return { maskedEmail: readString(record, 'maskedEmail', path), expiresAt: readString(record, 'expiresAt', path) };
}

/**
 * Creates the account API on top of an HTTP client that sends the access token of the session.
 * @param client HTTP client with session handling.
 * @returns The account API.
 */
export function createAccountApi(client: HttpClient): AccountApi {
  return {
    changePassword: (request, signal) => client.post('/api/account/password', request, parseAuthSession, signal),
    verifyEmail: (request, signal) => client.post('/api/account/email/verify', request, parseAuthSession, signal),
    resendEmailVerification: (signal) => client.post('/api/account/email/resend', undefined, parseEmailVerificationSent, signal),
  };
}
