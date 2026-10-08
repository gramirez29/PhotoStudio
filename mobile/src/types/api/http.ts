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

/** Options of the HTTP client factory. */
export interface HttpClientOptions {
  /** Fetch implementation; defaults to the global `fetch`. */
  readonly fetchFn?: FetchFunction;
  /** Request timeout in milliseconds; defaults to the client's default timeout. */
  readonly timeoutMs?: number;
}
