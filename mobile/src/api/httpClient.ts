import { isRecord } from './guards';

/** Default request timeout, in milliseconds. */
export const DEFAULT_TIMEOUT_MS = 15_000;

/** Error returned by the API (non-2xx status), carrying the stable `code` of the backend problem details. */
export class ApiError extends Error {
  /** HTTP status code. */
  public readonly status: number;

  /** Stable backend error code (for example `booking.invalid_transition`), when present. */
  public readonly code: string | null;

  /**
   * Creates the error.
   * @param status HTTP status code.
   * @param message Human-readable message.
   * @param code Stable backend error code, if any.
   */
  public constructor(status: number, message: string, code: string | null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

/** Minimal fetch signature, injectable for tests. */
export type FetchFunction = (input: string, init?: RequestInit) => Promise<Response>;

/** Converts an unknown response body into a typed value, throwing when the shape is invalid. */
export type ResponseParser<T> = (body: unknown) => T;

/** Typed HTTP client for the PhotoStudio API. */
export interface HttpClient {
  /**
   * Sends a GET request.
   * @param path Path relative to the base URL, starting with `/`.
   * @param parse Parser of the response body.
   * @param signal Optional signal to cancel the request.
   * @returns The parsed body.
   */
  get<T>(path: string, parse: ResponseParser<T>, signal?: AbortSignal): Promise<T>;

  /**
   * Sends a POST request with a JSON body.
   * @param path Path relative to the base URL, starting with `/`.
   * @param body Value serialized as JSON.
   * @param parse Parser of the response body.
   * @param signal Optional signal to cancel the request.
   * @returns The parsed body.
   */
  post<T>(path: string, body: unknown, parse: ResponseParser<T>, signal?: AbortSignal): Promise<T>;
}

/** Options of {@link createHttpClient}. */
export interface HttpClientOptions {
  /** Fetch implementation; defaults to the global `fetch`. */
  readonly fetchFn?: FetchFunction;
  /** Request timeout in milliseconds; defaults to {@link DEFAULT_TIMEOUT_MS}. */
  readonly timeoutMs?: number;
}

/**
 * Reads the body of a response: JSON when the content type says so, plain text otherwise.
 * @param response HTTP response.
 * @returns The parsed body, or null when it is empty.
 */
async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text.length === 0) {
    return null;
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('json')) {
    const parsed: unknown = JSON.parse(text);
    return parsed;
  }

  return text;
}

/**
 * Builds an {@link ApiError} from a failed response, using the problem details fields when available.
 * @param status HTTP status code.
 * @param body Response body.
 * @returns The error.
 */
export function toApiError(status: number, body: unknown): ApiError {
  if (isRecord(body)) {
    const detail = typeof body.detail === 'string' ? body.detail : null;
    const title = typeof body.title === 'string' ? body.title : null;
    const code = typeof body.code === 'string' ? body.code : null;
    return new ApiError(status, detail ?? title ?? `Request failed with status ${status}.`, code);
  }

  return new ApiError(status, `Request failed with status ${status}.`, null);
}

/**
 * Creates the HTTP client. Every request has a timeout and can also be cancelled by the caller.
 * @param baseUrl Base URL of the API, without trailing slash.
 * @param options Optional fetch implementation and timeout.
 * @returns The client.
 */
export function createHttpClient(baseUrl: string, options: HttpClientOptions = {}): HttpClient {
  const fetchFn: FetchFunction = options.fetchFn ?? ((input, init) => fetch(input, init));
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;

  /**
   * Sends a request and parses the response.
   * @param method HTTP method.
   * @param path Path relative to the base URL.
   * @param parse Parser of the response body.
   * @param body Optional JSON body.
   * @param signal Optional caller cancellation signal.
   * @returns The parsed body.
   */
  async function request<T>(
    method: 'GET' | 'POST',
    path: string,
    parse: ResponseParser<T>,
    body: unknown,
    signal: AbortSignal | undefined,
  ): Promise<T> {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    const forwardAbort = (): void => controller.abort();
    signal?.addEventListener('abort', forwardAbort);

    const headers: Record<string, string> = { Accept: 'application/json' };
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    try {
      const response = await fetchFn(`${baseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal,
      });

      const payload = await readBody(response);
      if (!response.ok) {
        throw toApiError(response.status, payload);
      }

      return parse(payload);
    } finally {
      clearTimeout(timeout);
      signal?.removeEventListener('abort', forwardAbort);
    }
  }

  return {
    get: (path, parse, signal) => request('GET', path, parse, undefined, signal),
    post: (path, body, parse, signal) => request('POST', path, parse, body, signal),
  };
}
