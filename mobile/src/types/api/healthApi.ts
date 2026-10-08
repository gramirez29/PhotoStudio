/** Readiness check of the backend. */
export interface HealthApi {
  /**
   * Asks the backend whether it is ready (API up and MongoDB reachable).
   * @param signal Optional signal to cancel the request.
   * @returns True when ready; false when the backend answers that it is not.
   */
  isReady(signal?: AbortSignal): Promise<boolean>;
}
