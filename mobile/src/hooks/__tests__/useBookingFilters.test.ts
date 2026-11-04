import { act, renderHook } from '@testing-library/react-native';
import { useBookingFilters } from '../useBookingFilters';

describe('useBookingFilters', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('starts with no filters and the panel closed', () => {
    const { result } = renderHook(() => useBookingFilters());

    expect(result.current.filter).toEqual({ status: null, name: '' });
    expect(result.current.open).toBe(false);
    expect(result.current.activeCount).toBe(0);
  });

  it('passes the text to the list only after the photographer pauses typing, but counts it at once', () => {
    const { result } = renderHook(() => useBookingFilters());

    act(() => result.current.setText('An'));
    act(() => result.current.setText('Ana'));
    expect(result.current.text).toBe('Ana');
    expect(result.current.activeCount).toBe(1);
    expect(result.current.filter.name).toBe('');

    act(() => jest.advanceTimersByTime(400));
    expect(result.current.filter.name).toBe('Ana');
  });

  it('applies the status at once, and choosing the same status again takes it off', () => {
    const { result } = renderHook(() => useBookingFilters());

    act(() => result.current.toggleStatus('Completed'));
    expect(result.current.filter.status).toBe('Completed');
    act(() => result.current.toggleStatus('Confirmed'));
    expect(result.current.filter.status).toBe('Confirmed');
    act(() => result.current.toggleStatus('Confirmed'));
    expect(result.current.filter.status).toBeNull();
  });

  it('clears the text and the status together, at once, and leaves the panel as it was', () => {
    const { result } = renderHook(() => useBookingFilters());
    act(() => result.current.togglePanel());
    act(() => result.current.setText('Ana'));
    act(() => result.current.toggleStatus('Expired'));
    act(() => jest.advanceTimersByTime(400));

    act(() => result.current.clear());

    expect(result.current.text).toBe('');
    expect(result.current.filter).toEqual({ status: null, name: '' });
    expect(result.current.activeCount).toBe(0);
    expect(result.current.open).toBe(true);
  });
});
