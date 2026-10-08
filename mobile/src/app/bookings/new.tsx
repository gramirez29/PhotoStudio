import type { ReactElement } from 'react';
import { NewBookingForm } from '../../components/NewBookingForm';
import { ScreenMessage } from '../../components/ScreenMessage';
import { env } from '../../config/env';

/**
 * Screen to create a booking. It needs the photographer to be configured in the app environment.
 * @returns The screen.
 */
export default function NewBookingScreen(): ReactElement {
  if (env.photographerId === null) {
    return <ScreenMessage message="Falta configurar EXPO_PUBLIC_PHOTOGRAPHER_ID en el archivo .env de la app." />;
  }

  return <NewBookingForm />;
}
