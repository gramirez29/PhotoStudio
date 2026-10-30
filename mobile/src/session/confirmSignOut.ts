import { Alert } from 'react-native';
import { signOutOfDevice } from '../push/pushSignOut';

/**
 * Asks whether to sign out of this device and, only if the photographer confirms, does it. The navigator takes them to the
 * login screen by itself. The menu and the verify-email screen share it.
 */
export function confirmSignOut(): void {
  Alert.alert('Cerrar sesión', '¿Quieres cerrar la sesión en este dispositivo?', [
    { text: 'Cancelar', style: 'cancel' },
    { text: 'Cerrar sesión', style: 'destructive', onPress: () => void signOutOfDevice() },
  ]);
}
