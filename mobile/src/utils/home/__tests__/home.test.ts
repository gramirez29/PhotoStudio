import { buildStatsPath, parseBookingAttention, parseBookingStats } from '../../../api/bookingsApi';
import type { BookingStatsResponse } from '../../../types/api/stats';
import { normalizeOptionalUrl } from '../../envNormalizers';
import { donutSegments, formatPercent } from '../donut';
import { periodRange } from '../periods';

/**
 * Builds counts by status for the tests; the statuses not given have zero.
 * @param counts Counts of the statuses that have bookings.
 * @returns The response, with the six statuses in the order of the backend.
 */
function statsOf(counts: Partial<Record<BookingStatsResponse['byStatus'][number]['status'], number>>): BookingStatsResponse {
  const byStatus = (['Tentative', 'Confirmed', 'Completed', 'Cancelled', 'Expired', 'ClientAbsent'] as const).map((status) => ({
    status,
    count: counts[status] ?? 0,
  }));
  return { total: byStatus.reduce((sum, entry) => sum + entry.count, 0), byStatus };
}

describe('donutSegments', () => {
  it('gives a slice only to the statuses that have bookings, in order, each with its share of the circle', () => {
    const segments = donutSegments(statsOf({ Confirmed: 3, Cancelled: 1 }));

    expect(segments.map((segment) => segment.status)).toEqual(['Confirmed', 'Cancelled']);
    expect(segments[0]).toMatchObject({ count: 3, fraction: 0.75, startAngle: 0, sweepAngle: 270 });
    expect(segments[1]).toMatchObject({ count: 1, fraction: 0.25, startAngle: 270, sweepAngle: 90 });
  });

  it('closes the circle: the slices add up to 360 degrees', () => {
    const segments = donutSegments(statsOf({ Tentative: 1, Confirmed: 1, Completed: 1 }));

    expect(segments.reduce((sum, segment) => sum + segment.sweepAngle, 0)).toBeCloseTo(360);
  });

  it('has no slices when there are no bookings', () => {
    expect(donutSegments(statsOf({}))).toEqual([]);
  });

  it('writes the share as a whole percentage', () => {
    expect(formatPercent(1 / 3)).toBe('33 %');
    expect(formatPercent(1)).toBe('100 %');
  });
});

describe('periodRange', () => {
  const now = new Date(2026, 10, 11, 15, 30);

  it('counts this month from its first day to the first day of the next', () => {
    const range = periodRange('thisMonth', now);

    expect(range).toEqual({ from: new Date(2026, 10, 1).toISOString(), to: new Date(2026, 11, 1).toISOString() });
  });

  it('counts last month, and the year turns over in January', () => {
    expect(periodRange('lastMonth', now)).toEqual({ from: new Date(2026, 9, 1).toISOString(), to: new Date(2026, 10, 1).toISOString() });
    expect(periodRange('lastMonth', new Date(2027, 0, 5))).toEqual({
      from: new Date(2026, 11, 1).toISOString(),
      to: new Date(2027, 0, 1).toISOString(),
    });
  });

  it('has no range for all the time', () => {
    expect(periodRange('all', now)).toBeNull();
  });
});

describe('stats API', () => {
  it('builds the path with the period encoded, or without one for all the bookings', () => {
    expect(buildStatsPath(null)).toBe('/api/bookings/stats');
    expect(buildStatsPath({ from: '2026-11-01T06:00:00.000Z', to: '2026-12-01T06:00:00.000Z' })).toBe(
      '/api/bookings/stats?from=2026-11-01T06%3A00%3A00.000Z&to=2026-12-01T06%3A00%3A00.000Z',
    );
  });

  it('reads the counts the backend sends', () => {
    const parsed = parseBookingStats({ total: 3, byStatus: [{ status: 'Confirmed', count: 2 }, { status: 'Cancelled', count: 1 }] });

    expect(parsed).toEqual({ total: 3, byStatus: [{ status: 'Confirmed', count: 2 }, { status: 'Cancelled', count: 1 }] });
  });

  it('refuses an answer that is not what the backend promises', () => {
    expect(() => parseBookingStats({ total: 'x', byStatus: [] })).toThrow();
    expect(() => parseBookingStats({ total: 1, byStatus: [{ status: 'Unknown', count: 1 }] })).toThrow();
    expect(() => parseBookingStats(null)).toThrow();
  });
});

describe('normalizeOptionalUrl', () => {
  it('keeps a web address, trimmed', () => {
    expect(normalizeOptionalUrl('  https://photosensestudio.com/studio ')).toBe('https://photosensestudio.com/studio');
  });

  it('drops what is missing, empty or not a web address', () => {
    expect(normalizeOptionalUrl(undefined)).toBeNull();
    expect(normalizeOptionalUrl('   ')).toBeNull();
    expect(normalizeOptionalUrl('javascript:alert(1)')).toBeNull();
    expect(normalizeOptionalUrl('photosensestudio.com')).toBeNull();
  });
});

describe('attention API', () => {
  it('reads the two counts the backend sends', () => {
    expect(parseBookingAttention({ pendingProofs: 2, expiringSoon: 1 })).toEqual({ pendingProofs: 2, expiringSoon: 1 });
  });

  it('refuses an answer that is not what the backend promises', () => {
    expect(() => parseBookingAttention({ pendingProofs: 2 })).toThrow();
    expect(() => parseBookingAttention([])).toThrow();
  });
});
