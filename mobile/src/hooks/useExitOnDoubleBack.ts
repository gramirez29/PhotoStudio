import { useFocusEffect } from 'expo-router';
import { useCallback, useRef } from 'react';
import { BackHandler, Platform, ToastAndroid } from 'react-native';
import { EXIT_CONFIRMATION_MS } from '../constants/home';

/**
 * Makes the back gesture of the phone ask before leaving the app, on the screen that uses it: the first time it shows "Presiona de
 * nuevo para salir" and only a second one, within a few seconds, leaves. It is for the home screen, the first of the stack, where
 * going back means leaving. It works only while the screen is the one in front, and only on Android, the only system with a back
 * gesture or button of its own.
 */
export function useExitOnDoubleBack(): void {
  const lastPress = useRef(0);

  useFocusEffect(
    useCallback(() => {
      if (Platform.OS !== 'android') {
        return undefined;
      }

      const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
        const now = Date.now();
        if (now - lastPress.current <= EXIT_CONFIRMATION_MS) {
          // Not handled here: the system leaves the app.
          return false;
        }

        lastPress.current = now;
        ToastAndroid.show('Presiona de nuevo para salir', ToastAndroid.SHORT);
        return true;
      });

      return () => subscription.remove();
    }, []),
  );
}
