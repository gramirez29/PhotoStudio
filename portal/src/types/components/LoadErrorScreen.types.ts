import type { LoadErrorView } from '../utils/apiErrors.types';

/** Props of the `LoadErrorScreen` component. */
export interface LoadErrorScreenProps {
  /** How the failure is explained. */
  readonly view: LoadErrorView;
  /** Called when the client asks to try again; only offered when the view allows it. */
  readonly onRetry: () => void;
}
