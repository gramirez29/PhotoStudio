import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useCountdown } from './useCountdown';

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('is idle until it is started', () => {
    const { result } = renderHook(() => useCountdown());

    expect(result.current.remaining).toBe(0);
  });

  it('counts down in whole seconds and stops at zero', () => {
    const { result } = renderHook(() => useCountdown());

    act(() => result.current.start(3));
    expect(result.current.remaining).toBe(3);

    act(() => vi.advanceTimersByTime(1000));
    expect(result.current.remaining).toBe(2);

    act(() => vi.advanceTimersByTime(5000));
    expect(result.current.remaining).toBe(0);
  });

  it('restarts from the new value', () => {
    const { result } = renderHook(() => useCountdown());
    act(() => result.current.start(10));
    act(() => vi.advanceTimersByTime(4000));

    act(() => result.current.start(60));

    expect(result.current.remaining).toBe(60);
  });

  it('shows the right time when the tab wakes up after sleeping', () => {
    const { result } = renderHook(() => useCountdown());
    act(() => result.current.start(60));

    // The timer did not run for 45 seconds; the next tick must still land on 15.
    act(() => {
      vi.setSystemTime(Date.now() + 45_000);
      vi.advanceTimersByTime(250);
    });

    expect(result.current.remaining).toBe(15);
  });
});
