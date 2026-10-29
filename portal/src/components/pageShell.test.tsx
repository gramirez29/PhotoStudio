import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PageShell } from './PageShell';

describe('PageShell', () => {
  it('opens every screen with the band of the studio: its logo and its name', () => {
    const { container } = render(<PageShell>contenido</PageShell>);

    const band = container.querySelector('header.site-header');
    expect(band).not.toBeNull();
    expect(band).toHaveTextContent('PhotoSense Studio');
    // The logo is decoration: the name next to it already says what it is.
    const logo = band?.querySelector('img');
    expect(logo).toHaveAttribute('alt', '');
    expect(logo?.getAttribute('src')).toMatch(/logo-mark/);
  });

  it('shows the content in the main column, after the band', () => {
    render(<PageShell>contenido</PageShell>);

    expect(screen.getByRole('main')).toHaveTextContent('contenido');
  });
});
