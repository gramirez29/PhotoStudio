import { describeApiHealth } from '../apiHealth';

describe('describeApiHealth', () => {
  it('reports a connection problem whenever the check failed', () => {
    expect(describeApiHealth(true, undefined)).toEqual({ label: 'Sin conexión con el servidor', color: 'danger' });
    expect(describeApiHealth(true, true).color).toBe('danger');
  });

  it('reports a ready backend', () => {
    expect(describeApiHealth(false, true)).toEqual({ label: 'Servidor conectado', color: 'success' });
  });

  it('reports a reachable backend whose database is down', () => {
    expect(describeApiHealth(false, false)).toEqual({
      label: 'Servidor disponible, base de datos sin conexión',
      color: 'warning',
    });
  });

  it('reports that it is still checking while there is no answer', () => {
    expect(describeApiHealth(false, undefined)).toEqual({
      label: 'Verificando conexión con el servidor…',
      color: 'textSecondary',
    });
  });
});
