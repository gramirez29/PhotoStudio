/** How a notification reads in the inbox. */
export interface NotificationText {
  /** Short heading, for example "Sesión próxima". */
  readonly title: string;
  /** One line with what the photographer needs to know. */
  readonly detail: string;
}
