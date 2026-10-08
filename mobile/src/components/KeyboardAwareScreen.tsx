import type { ReactElement } from 'react';
import { KeyboardAvoidingView, ScrollView, StyleSheet } from 'react-native';
import { spacing } from '../theme/tokens';
import type { KeyboardAwareScreenProps } from '../types/components/KeyboardAwareScreen.types';

/**
 * Screen container for forms that keeps the field being typed visible above the keyboard.
 *
 * The container shrinks to the space the keyboard leaves free (`KeyboardAvoidingView` with `padding`) and the content
 * scrolls inside it. `padding` is needed on Android as well as on iOS: Android draws the app edge to edge, so it no longer
 * resizes the window when the keyboard opens, and without this the keyboard covers the lower half of the screen. Taps keep
 * working while the keyboard is open, and dragging the form closes it.
 * @param props Component props.
 * @returns The container with its content.
 */
export function KeyboardAwareScreen({ children, align = 'center' }: KeyboardAwareScreenProps): ReactElement {
  return (
    <KeyboardAvoidingView style={styles.flex} behavior="padding">
      <ScrollView
        style={styles.flex}
        contentContainerStyle={[styles.content, align === 'top' && styles.top]}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
        automaticallyAdjustKeyboardInsets
      >
        {children}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/** Styles of the container. */
const styles = StyleSheet.create({
  flex: {
    flex: 1,
  },
  content: {
    flexGrow: 1,
    gap: spacing.md,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  top: {
    justifyContent: 'flex-start',
    padding: spacing.md,
  },
});
