import { useEffect, useState, type ReactElement } from 'react';
import { useStudioSession } from '../hooks/useStudioSession';
import type { PendingCode } from '../types/utils/studio.types';
import { readPendingCode } from '../utils/pendingCode';
import { LoadErrorScreen } from './LoadErrorScreen';
import { LoadingScreen } from './LoadingScreen';
import { StudioCodeForm } from './StudioCodeForm';
import { StudioGalleries } from './StudioGalleries';
import { StudioGalleryDetail } from './StudioGalleryDetail';
import { StudioLoginForm } from './StudioLoginForm';
import type { StudioPageProps } from '../types/components/StudioComponents.types';
import { describeLoadError } from '../utils/apiErrors';

/** Title of the tab while the web area is open. */
const TITLE = 'Tu estudio · PhotoSense Studio';

/**
 * The web area of the photographer, at `/studio`. It asks the server who is signed in and shows one of three things: the password,
 * the code (when one is waiting, which it remembers across a reload), the galleries, or one gallery when the address names it.
 * It tells search engines to keep out.
 * @param props Component props.
 * @returns The page.
 */
export function StudioPage({ galleryId }: StudioPageProps): ReactElement {
  const session = useStudioSession();
  const [pending, setPending] = useState<PendingCode | null>(() => readPendingCode());

  // The site is already closed to search engines by its headers and its robots file; the page says so itself as well.
  useEffect(() => {
    const previousTitle = document.title;
    const meta = document.createElement('meta');
    meta.name = 'robots';
    meta.content = 'noindex, nofollow';
    document.head.append(meta);
    document.title = TITLE;
    return () => {
      document.title = previousTitle;
      meta.remove();
    };
  }, []);

  if (session.isPending) {
    return <LoadingScreen message="Un momento…" />;
  }

  if (session.isError) {
    return <LoadErrorScreen view={describeLoadError(session.error)} onRetry={() => void session.refetch()} />;
  }

  if (session.data !== null) {
    return galleryId === undefined ? <StudioGalleries user={session.data} /> : <StudioGalleryDetail galleryId={galleryId} user={session.data} />;
  }

  if (pending !== null) {
    return <StudioCodeForm maskedEmail={pending.maskedEmail} onCancel={() => setPending(null)} />;
  }

  return <StudioLoginForm onCodeRequired={setPending} />;
}
