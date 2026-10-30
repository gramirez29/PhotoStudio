import { buildPagedPath, flattenPages } from '../pagination';

describe('buildPagedPath', () => {
  it('asks for the first page with only the page size', () => {
    expect(buildPagedPath('/api/bookings', null)).toBe('/api/bookings?limit=5');
  });

  it('continues after the cursor of the previous page, encoded so it cannot break the query', () => {
    expect(buildPagedPath('/api/notifications', '6390.0197a000')).toBe('/api/notifications?limit=5&cursor=6390.0197a000');
    expect(buildPagedPath('/api/notifications', 'a&b=c d')).toBe('/api/notifications?limit=5&cursor=a%26b%3Dc%20d');
  });
});

describe('flattenPages', () => {
  it('joins the pages in the order they came', () => {
    expect(flattenPages([{ items: [1, 2] }, { items: [3] }])).toEqual([1, 2, 3]);
  });

  it('gives nothing while no page has loaded, and for pages without items', () => {
    expect(flattenPages(undefined)).toEqual([]);
    expect(flattenPages([{ items: [] }])).toEqual([]);
  });
});
