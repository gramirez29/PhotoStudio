import type { Route } from '../types/utils/route.types';

/** Shortest and longest token the portal accepts in the address; the API issues 43 characters (256 bits in base64url). */
const TOKEN_PATTERN = /^\/b\/([A-Za-z0-9_-]{20,128})\/?$/;

/** A gallery of the web area: `/studio/galleries/{id}`, where the id is the GUID the API issues. */
const GALLERY_PATTERN = /^\/studio\/galleries\/([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\/?$/;

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
 * Decides which page an address shows: the main page at the root, the web area of the photographer at `/studio` and one of its
 * galleries at `/studio/galleries/{id}`, a booking
 * when the path is a link, or "not found".
 * @param pathname Path of the page.
 * @returns The route.
 */
export function resolveRoute(pathname: string): Route {
  if (pathname === '/' || pathname === '') {
    return { kind: 'home' };
  }

  if (pathname === '/studio' || pathname === '/studio/') {
    return { kind: 'studio' };
  }

  const galleryId = GALLERY_PATTERN.exec(pathname)?.[1];
  if (galleryId !== undefined) {
    return { kind: 'studioGallery', galleryId: galleryId.toLowerCase() };
  }

  const token = parseTokenFromPath(pathname);
  return token === null ? { kind: 'notFound' } : { kind: 'booking', token };
}
