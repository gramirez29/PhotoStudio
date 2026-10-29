import { act, renderHook } from '@testing-library/react-native';
import { useCooldown } from '../useCooldown';

describe('useCooldown', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('starts with nothing to wait', () => {
    const { result } = renderHook(() => useCooldown());

    expect(result.current.remaining).toBe(0);
  });

  it('counts down a second at a time and stops at zero', () => {
    const { result } = renderHook(() => useCooldown());

    act(() => result.current.start(3));
    expect(result.current.remaining).toBe(3);
    act(() => jest.advanceTimersByTime(1000));
    expect(result.current.remaining).toBe(2);

    // Each second schedules the next one after the screen is drawn again, so the time passes one second at a time.
    for (let second = 0; second < 5; second += 1) {
      act(() => jest.advanceTimersByTime(1000));
    }

    expect(result.current.remaining).toBe(0);
  });

  it('restarts from the new value, rounding fractions up', () => {
    const { result } = renderHook(() => useCooldown());

    act(() => result.current.start(10));
    act(() => jest.advanceTimersByTime(4000));
    act(() => result.current.start(1.2));

    expect(result.current.remaining).toBe(2);
  });
});
