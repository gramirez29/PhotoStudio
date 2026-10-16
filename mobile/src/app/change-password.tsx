import type { ReactElement } from 'react';
import { ChangePasswordForm } from '../components/ChangePasswordForm';

/**
 * Screen to change the password of the signed-in user.
 * @returns The screen.
 */
export default function ChangePasswordScreen(): ReactElement {
  return <ChangePasswordForm />;
}
