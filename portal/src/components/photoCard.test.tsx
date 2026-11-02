import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { PhotoCard } from './PhotoCard';

/**
 * Renders a card inside a list, as it is used.
 * @param props What differs from a plain card.
 */
function renderCard(props: Partial<Parameters<typeof PhotoCard>[0]> = {}): void {
  render(
    <ul>
      <PhotoCard image="https://s/thumb.jpg" name="IMG_0001.jpg" details={['4,2 MB']} openLabel="Ver IMG_0001.jpg" onOpen={vi.fn()} {...props} />
    </ul>,
  );
}

describe('the card of a photo', () => {
  it('shows the image on top and the name and what is known under it, and opens the photo when the image is pressed', () => {
    const onOpen = vi.fn();
    renderCard({ onOpen });

    expect(screen.getByRole('button', { name: 'Ver IMG_0001.jpg' }).querySelector('img')).toHaveAttribute('src', 'https://s/thumb.jpg');
    expect(screen.getByText('IMG_0001.jpg')).toHaveAttribute('title', 'IMG_0001.jpg');
    expect(screen.getByText('4,2 MB')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ver IMG_0001.jpg' }));
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('shows what is known about the photo in short lines, one under the other', () => {
    renderCard({ details: ['1250 × 3000 px · 4,2 MB', 'Tomada el 17 de octubre de 2026', 'Descargada 2 veces'] });

    expect(screen.getByText('1250 × 3000 px · 4,2 MB')).toBeInTheDocument();
    expect(screen.getByText('Tomada el 17 de octubre de 2026')).toBeInTheDocument();
    expect(screen.getByText('Descargada 2 veces')).toBeInTheDocument();
  });

  it('shows an icon in the place of the image when there is none, keeping the card whole', () => {
    renderCard({ image: null });

    const open = screen.getByRole('button', { name: 'Ver IMG_0001.jpg' });
    expect(open.querySelector('img')).toBeNull();
    expect(open.querySelector('svg')).not.toBeNull();
    expect(screen.getByText('IMG_0001.jpg')).toBeInTheDocument();
  });

  it('has the download icon inside the same card, named by what it does, and runs it', () => {
    const onClick = vi.fn();
    renderCard({ action: { label: 'Descargar IMG_0001.jpg', onClick } });

    const action = screen.getByRole('button', { name: 'Descargar IMG_0001.jpg' });
    expect(action.closest('li')).toBe(screen.getByRole('listitem'));
    expect(action).toHaveAttribute('title', 'Descargar IMG_0001.jpg');
    expect(action.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    fireEvent.click(action);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('can be disabled while something runs, and has no download icon when it has no action', () => {
    const { unmount } = render(
      <ul>
        <PhotoCard image={null} name="a.jpg" details={[]} openLabel="Ver a.jpg" onOpen={vi.fn()} action={{ label: 'Descargar a.jpg', onClick: vi.fn(), disabled: true }} />
      </ul>,
    );
    expect(screen.getByRole('button', { name: 'Descargar a.jpg' })).toBeDisabled();
    unmount();

    renderCard();
    expect(screen.queryByRole('button', { name: /Descargar/ })).toBeNull();
  });

  it('shows a label in the corner, warning when something is missing', () => {
    renderCard({ badge: { label: 'Sin final', warning: true } });

    expect(screen.getByText('Sin final')).toHaveClass('photo-card__badge', 'photo-card__badge--warning');
  });

  it('has a mark to pick the photo, with its name for screen readers', () => {
    const onChange = vi.fn();
    renderCard({ select: { checked: false, onChange, label: 'Seleccionar IMG_0001.jpg' } });

    const mark = screen.getByRole('checkbox', { name: 'Seleccionar IMG_0001.jpg' });
    fireEvent.click(mark);
    expect(onChange).toHaveBeenCalledWith(true);
  });
});
