import type { HttpClient, HttpClientOptions, RequestOptions, ResponseParser } from '../types/api/http';
import { expectRecord, readNullableString } from './guards';

/** Error answered by the API: the HTTP status and the stable `code` of the problem details, which the page branches on. */
export class ApiError extends Error {
  /** HTTP status of the answer. */
  public readonly status: number;

  /** Stable error code of the problem details (for example `portal.invalid_code`), or null when the answer had none. */
  public readonly code: string | null;

  /** Seconds the server asks to wait before trying again (`Retry-After`), or null. */
  public readonly retryAfterSeconds: number | null;

  /**
   * Creates the error.
   * @param status HTTP status of the answer.
   * @param message Description of the problem.
   * @param code Stable error code, if the answer had one.
   * @param retryAfterSeconds Seconds to wait, if the answer said so.
   */
  public constructor(status: number, message: string, code: string | null, retryAfterSeconds: number | null = null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

/** Thrown when the request never got an answer: no connection, DNS failure, or a browser that blocked it. */
export class NetworkError extends Error {
  /** Creates the error. */
  public constructor() {
    super('The request got no answer.');
    this.name = 'NetworkError';
  }
}

/**
 * Reads the problem details of an error answer. An answer that is not JSON (a proxy error page, for example) has no code.
 * @param response Error answer.
 * @returns The description and the stable code, when there are any.
 */
async function readProblem(response: Response): Promise<{ readonly message: string; readonly code: string | null }> {
  try {
    const record = expectRecord(await response.json(), 'problem');
    const detail = readNullableString(record, 'detail', 'problem');
    return { message: detail ?? `HTTP ${response.status}`, code: readNullableString(record, 'code', 'problem') };
  } catch {
    return { message: `HTTP ${response.status}`, code: null };
  }
}

/**
 * Reads the Retry-After header of an answer.
 * @param response Answer.
 * @returns The seconds to wait, or null when the header is missing or is not a number of seconds.
 */
function readRetryAfter(response: Response): number | null {
  const raw = response.headers.get('Retry-After');
  const seconds = raw === null ? Number.NaN : Number(raw);
  return Number.isFinite(seconds) && seconds >= 0 ? seconds : null;
}

/**
 * Creates the HTTP client of the portal. Every success is parsed against the contract; every failure becomes an
 * {@link ApiError} (the server answered) or a {@link NetworkError} (it did not).
 * @param baseUrl Address of the API, without a trailing slash.
 * @param options Options of the client.
 * @returns The client.
 */
export function createHttpClient(baseUrl: string, options: HttpClientOptions = {}): HttpClient {
  const fetchFn = options.fetchFn ?? ((input, init) => fetch(input, init));
  const withCredentials = options.withCredentials ?? false;

  /**
   * Sends a request and parses the answer.
   * @param method HTTP method.
   * @param path Path under the API address.
   * @param body Body to serialize, or undefined.
   * @param parse Parser of the response body.
   * @param request Signal and session token.
   * @returns The parsed response.
   */
  async function send<T>(
    method: 'GET' | 'POST',
    path: string,
    body: unknown,
    parse: ResponseParser<T>,
    request: RequestOptions,
  ): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    if (request.accessToken !== undefined) {
      headers.Authorization = `Bearer ${request.accessToken}`;
    }

    let response: Response;
    try {
      response = await fetchFn(`${baseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: request.signal,
        ...(withCredentials ? { credentials: 'include' as const } : {}),
      });
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        throw error;
      }

      throw new NetworkError();
    }

    if (!response.ok) {
      const problem = await readProblem(response);
      throw new ApiError(response.status, problem.message, problem.code, readRetryAfter(response));
    }

    // An answer with no content (204, such as signing out) has no body to read; its parser gets null.
    if (response.status === 204) {
      return parse(null);
    }

    return parse(await response.json());
  }

  return {
    get: (path, parse, request = {}) => send('GET', path, undefined, parse, request),
    post: (path, body, parse, request = {}) => send('POST', path, body, parse, request),
  };
}
