import type { ReactElement } from 'react';
import { LoginForm } from '../components/LoginForm';

/**
 * Login screen. The root layout shows it only while there is no session, and replaces it by the app once one exists.
 * @returns The screen.
 */
export default function LoginScreen(): ReactElement {
  return <LoginForm />;
}
