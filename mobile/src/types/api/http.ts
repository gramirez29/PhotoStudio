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

/** Status and body of a response, read before deciding whether the request succeeded. */
export interface RawHttpResponse {
  /** The response. */
  readonly response: Response;
  /** Body, parsed as JSON when the content type says so. */
  readonly payload: unknown;
}

/** Supplies the access token of the signed-in session to the HTTP client and renews it when the backend rejects it. */
export interface AccessTokenProvider {
  /**
   * Returns an access token that is still valid, renewing it first when it is about to expire.
   * @returns The token, or null when there is no session.
   */
  getAccessToken(): Promise<string | null>;

  /**
   * Called when the backend answered 401 to a request sent with `rejectedToken`. Renews the session once, no matter how
   * many requests were rejected at the same time.
   * @param rejectedToken Access token the backend refused.
   * @returns A token to retry with, or null when the session could not be renewed.
   */
  renewAfterRejection(rejectedToken: string): Promise<string | null>;
}

/** Options of the HTTP client factory. */
export interface HttpClientOptions {
  /** Fetch implementation; defaults to the global `fetch`. */
  readonly fetchFn?: FetchFunction;
  /** Request timeout in milliseconds; defaults to the client's default timeout. */
  readonly timeoutMs?: number;
  /** Session of the signed-in photographer; when present, every request carries its access token. */
  readonly auth?: AccessTokenProvider;
}
