import type { ReactElement } from 'react';
import { AuthPanel } from '../components/AuthPanel';

/**
 * Sign-in screen: log in or create an account. The root layout shows it only while there is no session, and replaces it by
 * the app once one exists.
 * @returns The screen.
 */
export default function LoginScreen(): ReactElement {
  return <AuthPanel />;
}
