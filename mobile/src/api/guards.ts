import type { JsonRecord } from '../types/api/json';

/** Thrown when a response body does not match the expected shape. */
export class ResponseShapeError extends Error {
  /**
   * Creates the error.
   * @param path Location of the invalid value, for example `booking.status`.
   * @param expected Description of the expected value.
   */
  public constructor(path: string, expected: string) {
    super(`Invalid response: ${path} should be ${expected}.`);
    this.name = 'ResponseShapeError';
  }
}

/**
 * Checks whether a value is a plain JSON object.
 * @param value Value to check.
 * @returns True for non-null, non-array objects.
 */
export function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Checks whether a string is one of the allowed literal values.
 * @param value String to check.
 * @param allowed Allowed values.
 * @returns True when the value is allowed.
 */
export function isOneOf<T extends string>(value: string, allowed: readonly T[]): value is T {
  return allowed.some((candidate) => candidate === value);
}

/**
 * Asserts that a value is a JSON object.
 * @param value Value to check.
 * @param path Location of the value, for error messages.
 * @returns The value typed as a record.
 */
export function expectRecord(value: unknown, path: string): JsonRecord {
  if (!isRecord(value)) {
    throw new ResponseShapeError(path, 'an object');
  }

  return value;
}

/**
 * Reads a required string property.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The string value.
 */
export function readString(record: JsonRecord, key: string, path: string): string {
  const value = record[key];
  if (typeof value !== 'string') {
    throw new ResponseShapeError(`${path}.${key}`, 'a string');
  }

  return value;
}

/**
 * Reads a string property that can be null or missing.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The string value, or null.
 */
export function readNullableString(record: JsonRecord, key: string, path: string): string | null {
  const value = record[key];
  if (value === null || value === undefined) {
    return null;
  }

  if (typeof value !== 'string') {
    throw new ResponseShapeError(`${path}.${key}`, 'a string or null');
  }

  return value;
}

/**
 * Reads a required finite number property.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The number value.
 */
export function readNumber(record: JsonRecord, key: string, path: string): number {
  const value = record[key];
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new ResponseShapeError(`${path}.${key}`, 'a number');
  }

  return value;
}

/**
 * Reads a required boolean property.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The boolean value.
 */
export function readBoolean(record: JsonRecord, key: string, path: string): boolean {
  const value = record[key];
  if (typeof value !== 'boolean') {
    throw new ResponseShapeError(`${path}.${key}`, 'a boolean');
  }

  return value;
}

/**
 * Reads a required array property.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The array, with elements still unknown.
 */
export function readArray(record: JsonRecord, key: string, path: string): readonly unknown[] {
  const value = record[key];
  if (!Array.isArray(value)) {
    throw new ResponseShapeError(`${path}.${key}`, 'an array');
  }

  const items: readonly unknown[] = value;
  return items;
}

/**
 * Reads a string property restricted to a set of literal values (a backend enum).
 * @param record Object to read from.
 * @param key Property name.
 * @param allowed Allowed values.
 * @param path Location of the object, for error messages.
 * @returns The literal value.
 */
export function readLiteral<T extends string>(record: JsonRecord, key: string, allowed: readonly T[], path: string): T {
  const value = readString(record, key, path);
  if (!isOneOf(value, allowed)) {
    throw new ResponseShapeError(`${path}.${key}`, `one of ${allowed.join(', ')}`);
  }

  return value;
}

/**
 * Asserts that a value is a JSON array.
 * @param value Value to check.
 * @param path Location of the value, for error messages.
 * @returns The array, with elements still unknown.
 */
export function expectArray(value: unknown, path: string): readonly unknown[] {
  if (!Array.isArray(value)) {
    throw new ResponseShapeError(path, 'an array');
  }

  const items: readonly unknown[] = value;
  return items;
}
