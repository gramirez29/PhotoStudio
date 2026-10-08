import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement, ReactNode } from 'react';

/** A query client for hook tests, with the wrapper that provides it. */
export interface QueryWrapper {
  /** Client used by the wrapper; call `clear()` after each test. */
  readonly client: QueryClient;
  /** Component to pass as `wrapper` to `renderHook`. */
  readonly Wrapper: ({ children }: { readonly children: ReactNode }) => ReactElement;
}

/**
 * Creates a query client for hook tests: no retries, so a failed request fails right away, and no cache lifetime, so no
 * garbage-collection timer is left running when the test ends.
 * @returns The client and the wrapper that provides it.
 */
export function createQueryWrapper(): QueryWrapper {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false, gcTime: 0 } },
  });

  /**
   * Provides the client to the hook under test.
   * @param props Component props.
   * @param props.children Hook under test.
   * @returns The provider.
   */
  function Wrapper({ children }: { readonly children: ReactNode }): ReactElement {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  }

  return { client, Wrapper };
}
