import { QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { createQueryClient } from './api/queryClient';
import { BookingPage } from './components/BookingPage';
import { HomePage } from './components/HomePage';
import { NotFoundScreen } from './components/NotFoundScreen';
import { PageShell } from './components/PageShell';
import { resolveRoute } from './utils/route';

/**
 * Root of the site. The main page is at `/`, a client's booking at `/b/:token`, and anything else is "not found". The address
 * is read once: every link inside the site is a full page load, so the route never changes while the page is open.
 * @returns The page.
 */
export default function App(): ReactElement {
  const [queryClient] = useState(createQueryClient);
  const [route] = useState(() => resolveRoute(window.location.pathname));

  let content: ReactElement;
  switch (route.kind) {
    case 'home':
      content = <HomePage />;
      break;
    case 'booking':
      content = <BookingPage token={route.token} />;
      break;
    case 'notFound':
      content = <NotFoundScreen />;
      break;
  }

  return (
    <QueryClientProvider client={queryClient}>
      <PageShell>{content}</PageShell>
    </QueryClientProvider>
  );
}
