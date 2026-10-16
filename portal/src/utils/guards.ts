/**
 * Checks that a string is one of the allowed values.
 * @param value String to check.
 * @param allowed Allowed values.
 * @returns True when the value is allowed; narrows its type to the union of the allowed ones.
 */
export function isOneOf<T extends string>(value: string, allowed: readonly T[]): value is T {
  return (allowed as readonly string[]).includes(value);
}
