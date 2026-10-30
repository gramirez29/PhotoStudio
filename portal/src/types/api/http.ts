/** Function that turns the unknown JSON body of a successful response into a typed value, or throws when it has the wrong shape. */
export type ResponseParser<T> = (body: unknown) => T;

/** Options of an HTTP client. */
export interface HttpClientOptions {
  /** Function that sends the request; defaults to the global `fetch`. Tests replace it. */
  readonly fetchFn?: typeof fetch;
  /** Whether requests carry the cookies of the API (`credentials: 'include'`). Only the web area of the photographer uses them. */
  readonly withCredentials?: boolean;
}

/** Options of one request. */
export interface RequestOptions {
  /** Signal to cancel the request. */
  readonly signal?: AbortSignal;
  /** Portal session token, sent as `Authorization: Bearer`. */
  readonly accessToken?: string;
}

/** Minimal HTTP client of the portal: it parses every response against the contract before handing it over. */
export interface HttpClient {
  /**
   * Sends a GET request.
   * @param path Path under the API address, starting with a slash.
   * @param parse Parser of the response body.
   * @param options Signal and session token of the request.
   * @returns The parsed response.
   */
  get<T>(path: string, parse: ResponseParser<T>, options?: RequestOptions): Promise<T>;

  /**
   * Sends a POST request with an optional JSON body.
   * @param path Path under the API address, starting with a slash.
   * @param body Body to serialize as JSON, or undefined to send none.
   * @param parse Parser of the response body.
   * @param options Signal and session token of the request.
   * @returns The parsed response.
   */
  post<T>(path: string, body: unknown, parse: ResponseParser<T>, options?: RequestOptions): Promise<T>;
}
