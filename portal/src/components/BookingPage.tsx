import { useState, type ReactElement } from 'react';
import { usePortalBooking } from '../hooks/usePortalBooking';
import type { PortalSession } from '../types/api/portal';
import type { BookingPageProps } from '../types/components/BookingPage.types';
import { describeLoadError } from '../utils/apiErrors';
import { BookingView } from './BookingView';
import { ContractFlow } from './ContractFlow';
import { LoadErrorScreen } from './LoadErrorScreen';
import { LoadingScreen } from './LoadingScreen';
import { ProofFlow } from './ProofFlow';

/**
 * Screen of a booking link: loads the booking and shows it, or says why it cannot. The session that comes from confirming a
 * code lives here, so the contract and the payment proof share it and the client confirms their email once, not once per action.
 * @param props Component props.
 * @returns The screen.
 */
export function BookingPage({ token }: BookingPageProps): ReactElement {
  const query = usePortalBooking(token);
  const [session, setSession] = useState<PortalSession | null>(null);

  if (query.isPending) {
    return <LoadingScreen />;
  }

  if (query.isError) {
    return <LoadErrorScreen view={describeLoadError(query.error)} onRetry={() => void query.refetch()} />;
  }

  return (
    <BookingView
      booking={query.data}
      action={
        <>
          <ContractFlow token={token} booking={query.data} session={session} onSessionChange={setSession} />
          <ProofFlow token={token} booking={query.data} session={session} onSessionChange={setSession} />
        </>
      }
    />
  );
}
