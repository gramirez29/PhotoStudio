import { buildFilterParams, countActiveCriteria, isFilterActive, NO_BOOKING_FILTER, normalizeNameFilter } from '../bookingFilters';

describe('normalizeNameFilter', () => {
  it('drops the spaces around the text and cuts what the backend would not accept', () => {
    expect(normalizeNameFilter('  Ana  ')).toBe('Ana');
    expect(normalizeNameFilter('   ')).toBe('');
    expect(normalizeNameFilter('a'.repeat(200))).toHaveLength(80);
  });
});

describe('isFilterActive and countActiveCriteria', () => {
  it('count nothing for the empty filter, and one for each criterion in use', () => {
    expect(isFilterActive(NO_BOOKING_FILTER)).toBe(false);
    expect(countActiveCriteria(NO_BOOKING_FILTER)).toBe(0);
    expect(countActiveCriteria({ status: 'Completed', name: '' })).toBe(1);
    expect(countActiveCriteria({ status: null, name: ' ana ' })).toBe(1);
    expect(countActiveCriteria({ status: 'Completed', name: 'ana' })).toBe(2);
    expect(isFilterActive({ status: null, name: '   ' })).toBe(false);
    expect(isFilterActive({ status: 'Expired', name: '' })).toBe(true);
  });
});

describe('buildFilterParams', () => {
  it('leaves out the criteria that are not used', () => {
    expect(buildFilterParams(NO_BOOKING_FILTER)).toEqual({});
    expect(buildFilterParams({ status: 'Confirmed', name: '' })).toEqual({ status: 'Confirmed' });
    expect(buildFilterParams({ status: 'Confirmed', name: ' Ana ' })).toEqual({ status: 'Confirmed', name: 'Ana' });
  });
});
