import type { ReactElement } from 'react';
import { VerifyEmailForm } from '../components/VerifyEmailForm';

/**
 * Screen where a user who has not verified their email types the code. It is the only screen of the app until they do.
 * @returns The screen.
 */
export default function VerifyEmailScreen(): ReactElement {
  return <VerifyEmailForm />;
}
