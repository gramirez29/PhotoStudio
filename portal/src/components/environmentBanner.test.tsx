import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../config/env', () => ({ apiUrl: 'https://api.example.test', testEnvironment: 'develop' }));

describe('EnvironmentBanner on a test build', () => {
  it('says this is a test site and that the data is not real', async () => {
    const { EnvironmentBanner } = await import('./EnvironmentBanner');

    render(<EnvironmentBanner />);

    expect(screen.getByRole('note')).toHaveTextContent('Ambiente de pruebas (develop). Los datos no son reales.');
  });

  it('is part of every page', async () => {
    const { PageShell } = await import('./PageShell');

    render(<PageShell>contenido</PageShell>);

    expect(screen.getByRole('note')).toBeInTheDocument();
    expect(screen.getByText('contenido')).toBeInTheDocument();
  });
});
