import { MAX_NAME_FILTER_LENGTH } from '../constants/pagination';
import type { BookingListFilter } from '../types/api/booking';

/** The filter that lets every booking through. */
export const NO_BOOKING_FILTER: BookingListFilter = { status: null, name: '' };

/**
 * Cleans what the photographer typed in the search box: the spaces around it are dropped and it is cut to what the backend accepts.
 * @param name Text of the search box.
 * @returns The text to search for; empty when there is nothing to search.
 */
export function normalizeNameFilter(name: string): string {
  return name.trim().slice(0, MAX_NAME_FILTER_LENGTH).trim();
}

/**
 * Tells whether a filter leaves some bookings out.
 * @param filter The filter.
 * @returns True when a status is chosen or there is text to search.
 */
export function isFilterActive(filter: BookingListFilter): boolean {
  return filter.status !== null || normalizeNameFilter(filter.name) !== '';
}

/**
 * Counts the criteria a filter applies, for the mark on the filter button.
 * @param filter The filter.
 * @returns 0, 1 or 2.
 */
export function countActiveCriteria(filter: BookingListFilter): number {
  return (filter.status !== null ? 1 : 0) + (normalizeNameFilter(filter.name) !== '' ? 1 : 0);
}

/**
 * Turns a filter into the query parameters of the list; a criterion that is not used is left out.
 * @param filter The filter.
 * @returns The parameters to add to the request.
 */
export function buildFilterParams(filter: BookingListFilter): Readonly<Record<string, string>> {
  const name = normalizeNameFilter(filter.name);
  return {
    ...(filter.status !== null ? { status: filter.status } : {}),
    ...(name !== '' ? { name } : {}),
  };
}
