import { QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { createQueryClient } from './api/queryClient';
import { BookingPage } from './components/BookingPage';
import { HomePage } from './components/HomePage';
import { NotFoundScreen } from './components/NotFoundScreen';
import { PageShell } from './components/PageShell';
import { StudioPage } from './components/StudioPage';
import { resolveRoute } from './utils/route';

/**
 * Root of the site. The main page is at `/`, the web area of the photographer at `/studio`, a client's booking at `/b/:token`,
 * and anything else is "not found". The address
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
    case 'studio':
      content = <StudioPage />;
      break;
    case 'studioGallery':
      content = <StudioPage galleryId={route.galleryId} />;
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
