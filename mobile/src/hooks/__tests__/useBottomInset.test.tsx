import { render, renderHook, screen } from '@testing-library/react-native';
import { StyleSheet, Text } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { KeyboardAwareScreen } from '../../components/KeyboardAwareScreen';
import { spacing } from '../../theme/tokens';
import { useBottomInset } from '../useBottomInset';

/** Metrics of a phone whose navigation bar covers the last 24 points of the screen. */
const METRICS = { frame: { x: 0, y: 0, width: 360, height: 780 }, insets: { top: 24, left: 0, right: 0, bottom: 24 } };

describe('useBottomInset', () => {
  it('gives what the system draws over the bottom edge', () => {
    const { result } = renderHook(() => useBottomInset(), {
      wrapper: ({ children }) => <SafeAreaProvider initialMetrics={METRICS}>{children}</SafeAreaProvider>,
    });

    expect(result.current).toBe(24);
  });

  it('gives nothing where no safe area is provided', () => {
    const { result } = renderHook(() => useBottomInset());

    expect(result.current).toBe(0);
  });
});

describe('KeyboardAwareScreen at the bottom edge', () => {
  /**
   * Reads the padding the form leaves at its end.
   * @param align Where the content sits.
   * @param withInset Whether the system covers the bottom edge.
   * @returns The bottom padding of the scrolled content.
   */
  function bottomPadding(align: 'center' | 'top', withInset: boolean): number | string | undefined {
    render(
      <SafeAreaProvider initialMetrics={withInset ? METRICS : { ...METRICS, insets: { ...METRICS.insets, bottom: 0 } }}>
        <KeyboardAwareScreen align={align}>
          <Text>campo</Text>
        </KeyboardAwareScreen>
      </SafeAreaProvider>,
    );
    const scroll = screen.UNSAFE_getByProps({ keyboardShouldPersistTaps: 'handled' });
    return StyleSheet.flatten(scroll.props.contentContainerStyle).paddingBottom;
  }

  it('leaves room for the navigation bar after its normal padding', () => {
    expect(bottomPadding('top', true)).toBe(spacing.md + 24);
  });

  it('keeps its normal padding when the system covers nothing', () => {
    expect(bottomPadding('center', false)).toBe(spacing.lg);
  });
});
