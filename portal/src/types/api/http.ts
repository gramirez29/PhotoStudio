/** Function that turns the unknown JSON body of a successful response into a typed value, or throws when it has the wrong shape. */
export type ResponseParser<T> = (body: unknown) => T;

/** Options of an HTTP client. */
export interface HttpClientOptions {
  /** Function that sends the request; defaults to the global `fetch`. Tests replace it. */
  readonly fetchFn?: typeof fetch;
}

/** Minimal HTTP client of the portal: it parses every response against the contract before handing it over. */
export interface HttpClient {
  /**
   * Sends a GET request.
   * @param path Path under the API address, starting with a slash.
   * @param parse Parser of the response body.
   * @param signal Optional signal to cancel the request.
   * @returns The parsed response.
   */
  get<T>(path: string, parse: ResponseParser<T>, signal?: AbortSignal): Promise<T>;
}
