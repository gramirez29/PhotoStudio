import type { Route } from '../types/utils/route.types';

/** Shortest and longest token the portal accepts in the address; the API issues 43 characters (256 bits in base64url). */
const TOKEN_PATTERN = /^\/b\/([A-Za-z0-9_-]{20,128})\/?$/;

/**
 * Reads the token of the booking link from the path of the page, for example `/b/Zm9v…`. A path that has any other shape,
 * or a token with characters the API never issues, yields nothing, so junk addresses never reach the API.
 * @param pathname Path of the page.
 * @returns The token, or null when the path is not a booking link.
 */
export function parseTokenFromPath(pathname: string): string | null {
  return TOKEN_PATTERN.exec(pathname)?.[1] ?? null;
}

/**
 * Decides which page an address shows: the main page at the root, a booking when the path is a link, or "not found".
 * @param pathname Path of the page.
 * @returns The route.
 */
export function resolveRoute(pathname: string): Route {
  if (pathname === '/' || pathname === '') {
    return { kind: 'home' };
  }

  const token = parseTokenFromPath(pathname);
  return token === null ? { kind: 'notFound' } : { kind: 'booking', token };
}
