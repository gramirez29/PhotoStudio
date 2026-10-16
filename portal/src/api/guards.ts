import { isOneOf } from '../utils/guards';

/** A JSON object whose properties are not typed yet. */
export type JsonRecord = Readonly<Record<string, unknown>>;

/** Thrown when a response does not have the shape the contract promises; it means the page and the API disagree. */
export class ResponseShapeError extends Error {
  /** Location of the offending value, for example `booking.payments[0].status`. */
  public readonly path: string;

  /**
   * Creates the error.
   * @param path Location of the offending value.
   * @param expected What was expected there.
   */
  public constructor(path: string, expected: string) {
    super(`Unexpected response: ${path} should be ${expected}.`);
    this.name = 'ResponseShapeError';
    this.path = path;
  }
}

/**
 * Checks that a value is a JSON object.
 * @param value Value to check.
 * @param path Location of the value, for error messages.
 * @returns The value as a record.
 */
export function expectRecord(value: unknown, path: string): JsonRecord {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new ResponseShapeError(path, 'an object');
  }

  return value as JsonRecord;
}

/**
 * Reads a required string property.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The string.
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
 * @returns The string, or null.
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
 * @returns The number.
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
 * @returns The boolean.
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
 * @returns The array, whose elements are not typed yet.
 */
export function readArray(record: JsonRecord, key: string, path: string): readonly unknown[] {
  const value = record[key];
  if (!Array.isArray(value)) {
    throw new ResponseShapeError(`${path}.${key}`, 'an array');
  }

  return value;
}

/**
 * Reads a required string property that must be one of the allowed values.
 * @param record Object to read from.
 * @param key Property name.
 * @param allowed Allowed values.
 * @param path Location of the object, for error messages.
 * @returns The value, typed as the union of the allowed ones.
 */
export function readLiteral<T extends string>(record: JsonRecord, key: string, allowed: readonly T[], path: string): T {
  const value = readString(record, key, path);
  if (!isOneOf(value, allowed)) {
    throw new ResponseShapeError(`${path}.${key}`, `one of ${allowed.join(', ')}`);
  }

  return value;
}
