import type { ReactElement } from 'react';
import { usePortalBooking } from '../hooks/usePortalBooking';
import type { BookingPageProps } from '../types/components/BookingPage.types';
import { describeLoadError } from '../utils/apiErrors';
import { BookingView } from './BookingView';
import { LoadErrorScreen } from './LoadErrorScreen';
import { LoadingScreen } from './LoadingScreen';
import { ProofFlow } from './ProofFlow';

/**
 * Screen of a booking link: loads the booking and shows it, or says why it cannot.
 * @param props Component props.
 * @returns The screen.
 */
export function BookingPage({ token }: BookingPageProps): ReactElement {
  const query = usePortalBooking(token);

  if (query.isPending) {
    return <LoadingScreen />;
  }

  if (query.isError) {
    return <LoadErrorScreen view={describeLoadError(query.error)} onRetry={() => void query.refetch()} />;
  }

  return <BookingView booking={query.data} action={<ProofFlow token={token} booking={query.data} />} />;
}
