import { SERVICES } from '../services';

describe('SERVICES', () => {
  it('offers exactly the services of the photographer', () => {
    expect([...SERVICES].sort()).toEqual(
      [
        'Compromiso',
        'Pre-Boda',
        'Pre-Quinceaños',
        'Producto',
        'Retrato Exterior',
        'Retrato Familiar',
        'Retrato Individual',
        'Retrato Moda',
      ].sort(),
    );
  });

  it('is sorted alphabetically in Spanish', () => {
    const sorted = [...SERVICES].sort((left, right) => left.localeCompare(right, 'es'));

    expect([...SERVICES]).toEqual(sorted);
  });

  it('has no repeated service', () => {
    expect(new Set(SERVICES).size).toBe(SERVICES.length);
  });
});
