import { render, screen } from '@testing-library/react-native';
import { KeyboardAvoidingView, ScrollView, StyleSheet, Text } from 'react-native';
import { KeyboardAwareScreen } from '../KeyboardAwareScreen';

describe('KeyboardAwareScreen', () => {
  it('renders its content', () => {
    render(
      <KeyboardAwareScreen>
        <Text>contenido</Text>
      </KeyboardAwareScreen>,
    );

    expect(screen.getByText('contenido')).toBeTruthy();
  });

  it('shrinks to the space above the keyboard on both platforms, which Android needs because it draws edge to edge', () => {
    render(
      <KeyboardAwareScreen>
        <Text>contenido</Text>
      </KeyboardAwareScreen>,
    );

    expect(screen.UNSAFE_getByType(KeyboardAvoidingView).props.behavior).toBe('padding');
  });

  it('keeps taps working while the keyboard is open and lets a drag close it', () => {
    render(
      <KeyboardAwareScreen>
        <Text>contenido</Text>
      </KeyboardAwareScreen>,
    );

    const scroll = screen.UNSAFE_getByType(ScrollView);
    expect(scroll.props.keyboardShouldPersistTaps).toBe('handled');
    expect(scroll.props.keyboardDismissMode).toBe('on-drag');
  });

  it('centers short content by default, as the sign-in needs', () => {
    render(
      <KeyboardAwareScreen>
        <Text>contenido</Text>
      </KeyboardAwareScreen>,
    );

    const style = StyleSheet.flatten(screen.UNSAFE_getByType(ScrollView).props.contentContainerStyle);
    expect(style.justifyContent).toBe('center');
  });

  it('keeps the content at the top for forms that read from the top down', () => {
    render(
      <KeyboardAwareScreen align="top">
        <Text>contenido</Text>
      </KeyboardAwareScreen>,
    );

    const style = StyleSheet.flatten(screen.UNSAFE_getByType(ScrollView).props.contentContainerStyle);
    expect(style.justifyContent).toBe('flex-start');
  });
});
