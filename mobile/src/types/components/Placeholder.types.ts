/** Props of the `Placeholder` component. */
export interface PlaceholderProps {
  /** Message shown to the photographer. */
  readonly message: string;
  /** Optional retry handler; when present a button is shown. */
  readonly onRetry?: () => void;
}
