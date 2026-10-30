import { PAGE_SIZE } from '../constants/pagination';

/**
 * Builds the path of a page of a list: the page size and, after the first page, the cursor the backend gave with the page before.
 * @param path Path of the list, without a query string.
 * @param cursor The `nextCursor` of the previous page, or null for the first page.
 * @returns The path with its query string.
 */
export function buildPagedPath(path: string, cursor: string | null): string {
  const query = `limit=${PAGE_SIZE}`;
  return cursor === null ? `${path}?${query}` : `${path}?${query}&cursor=${encodeURIComponent(cursor)}`;
}

/**
 * Joins the items of every page loaded so far, in the order the pages came: the newest first.
 * @param pages The loaded pages, or undefined while nothing has loaded.
 * @returns All their items.
 */
export function flattenPages<T>(pages: readonly { readonly items: readonly T[] }[] | undefined): readonly T[] {
  return pages?.flatMap((page) => page.items) ?? [];
}
