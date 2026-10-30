/** Props of the `LoadMoreFooter` component. */
export interface LoadMoreFooterProps {
  /** Whether the backend has another page after the ones shown. */
  readonly hasMore: boolean;
  /** Whether the next page is being loaded right now. */
  readonly isLoading: boolean;
  /** Whether the last attempt to load the next page failed. */
  readonly failed: boolean;
  /** Called when the photographer taps the button to load the next page. */
  readonly onLoadMore: () => void;
}
