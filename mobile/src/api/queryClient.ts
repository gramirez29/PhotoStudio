import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './httpClient';

/** Server responses considered fresh for this long, in milliseconds. */
export const STALE_TIME_MS = 30_000;

/**
 * Decides whether a failed query should be retried: client errors (4xx) are not transient, so they are not retried.
 * @param failureCount Number of failures so far.
 * @param error Error of the last attempt.
 * @returns True to retry.
 */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false;
  }

  return failureCount < 2;
}

/**
 * Creates the TanStack Query client with the app defaults.
 * @returns A new query client.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: STALE_TIME_MS,
        retry: shouldRetry,
      },
    },
  });
}
