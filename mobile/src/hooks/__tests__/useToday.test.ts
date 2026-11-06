import { act, renderHook } from '@testing-library/react-native';
import { useToday } from '../useToday';

describe('useToday', () => {
  beforeEach(() => {
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 23, 59, 0));
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('is the midnight that starts today', () => {
    const { result } = renderHook(() => useToday());

    expect(result.current).toEqual(new Date(2026, 10, 11));
  });

  it('keeps the very same object while the day does not change, so what depends on it does not draw again', () => {
    const { result } = renderHook(() => useToday());
    const first = result.current;

    act(() => jest.advanceTimersByTime(30_000));

    expect(result.current).toBe(first);
  });

  it('moves on to the next day after midnight', () => {
    const { result } = renderHook(() => useToday());

    act(() => jest.advanceTimersByTime(90_000));

    expect(result.current).toEqual(new Date(2026, 10, 12));
  });

  it('stops looking at the clock when it is no longer used', () => {
    const { unmount } = renderHook(() => useToday());

    unmount();

    expect(jest.getTimerCount()).toBe(0);
  });
});
