import { QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { createQueryClient } from './api/queryClient';
import { BookingPage } from './components/BookingPage';
import { NoLinkScreen } from './components/NoLinkScreen';
import { PageShell } from './components/PageShell';
import { parseTokenFromPath } from './utils/route';

/**
 * Root of the portal. There is one route, `/b/:token`; any other address explains that the page is opened from the link.
 * @returns The page.
 */
export default function App(): ReactElement {
  const [queryClient] = useState(createQueryClient);
  const [token] = useState(() => parseTokenFromPath(window.location.pathname));

  return (
    <QueryClientProvider client={queryClient}>
      <PageShell>{token === null ? <NoLinkScreen /> : <BookingPage token={token} />}</PageShell>
    </QueryClientProvider>
  );
}
