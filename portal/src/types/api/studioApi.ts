import type { StudioCodeSent, StudioGalleryPage, StudioLoginResult, StudioUser } from './studio';

/** Endpoints of the web area of the photographer. */
export interface StudioApi {
  /**
   * Asks who is signed in.
   * @param signal Optional signal to cancel the request.
   * @returns The user; the request fails with a 401 when nobody is.
   */
  getSession(signal?: AbortSignal): Promise<StudioUser>;

  /**
   * Gives the password. A remembered browser gets its session; any other gets a code in the email.
   * @param username Username as typed.
   * @param password Password as typed.
   * @returns The session, or where the code went.
   */
  login(username: string, password: string): Promise<StudioLoginResult>;

  /**
   * Gives the code from the email and starts the session.
   * @param code The six digits.
   * @param rememberDevice Whether to skip the code in this browser for 30 days.
   * @returns The user who signed in.
   */
  verifyCode(code: string, rememberDevice: boolean): Promise<StudioUser>;

  /**
   * Asks for another code.
   * @returns Where the code went.
   */
  resendCode(): Promise<StudioCodeSent>;

  /**
   * Signs out, closing the session on the server.
   * @param forgetDevice Whether to also forget this browser, so the next sign-in asks for the code again.
   * @returns A promise that resolves when the session is closed.
   */
  logout(forgetDevice: boolean): Promise<void>;

  /**
   * Loads one page of the galleries of the photographer.
   * @param cursor The `nextCursor` of the previous page, or null for the first page.
   * @param signal Optional signal to cancel the request.
   * @returns The page.
   */
  listGalleries(cursor: string | null, signal?: AbortSignal): Promise<StudioGalleryPage>;
}
