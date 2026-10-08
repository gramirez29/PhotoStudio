import type { ReactElement } from 'react';
import { NewBookingForm } from '../../components/NewBookingForm';

/**
 * Screen to create a booking. The photographer is the one who is signed in, so there is nothing to configure.
 * @returns The screen.
 */
export default function NewBookingScreen(): ReactElement {
  return <NewBookingForm />;
}
