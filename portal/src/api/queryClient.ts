import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './httpClient';

/** Times a failed request is repeated before the error is shown. */
export const MAX_RETRIES = 2;

/**
 * Decides whether a failed request is repeated. An answer in the 4xx range means the request itself is wrong (a link that is
 * not valid, a rate limit), so repeating it changes nothing; a network or server failure may pass on the next try.
 * @param failureCount Failures so far.
 * @param error Error of the last attempt.
 * @returns True to try again.
 */
export function shouldRetry(failureCount: number, error: Error): boolean {
  return !(error instanceof ApiError && error.status < 500) && failureCount < MAX_RETRIES;
}

/**
 * Creates the query client of the page. The booking is refreshed when the client comes back to the tab, which is when a
 * payment may have been verified in the meantime.
 * @returns The client.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: shouldRetry, refetchOnWindowFocus: true } } });
}
