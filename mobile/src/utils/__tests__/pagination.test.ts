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

describe('buildPagedPath with filters', () => {
  it('puts the filters between the page size and the cursor, encoded', () => {
    expect(buildPagedPath('/api/bookings', null, { status: 'Completed', name: 'Ana & María' })).toBe(
      '/api/bookings?limit=5&status=Completed&name=Ana%20%26%20Mar%C3%ADa',
    );
    expect(buildPagedPath('/api/bookings', 'c1', { status: 'Expired' })).toBe('/api/bookings?limit=5&status=Expired&cursor=c1');
  });
});
