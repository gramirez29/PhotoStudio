import type { ReactElement } from 'react';
import { SettingsList } from '../components/SettingsList';

/**
 * Settings screen: the appearance of the app and the account.
 * @returns The screen.
 */
export default function SettingsScreen(): ReactElement {
  return <SettingsList />;
}
