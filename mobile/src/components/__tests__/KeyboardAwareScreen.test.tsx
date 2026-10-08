import { render, screen } from '@testing-library/react-native';
import { KeyboardAvoidingView, ScrollView, Text } from 'react-native';
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
});
