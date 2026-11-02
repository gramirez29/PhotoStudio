import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { ViewerPhoto } from '../types/components/PhotoViewer.types';
import { PhotoViewer } from './PhotoViewer';

const PHOTOS: readonly ViewerPhoto[] = [
  { id: 'a', name: 'IMG_0001.jpg', caption: '4,2 MB', placeholder: 'https://s/thumb-a' },
  { id: 'b', name: 'IMG_0002.jpg', caption: '', placeholder: null },
];

describe('the viewer of photos', () => {
  it('shows the small image while the real photo loads, and then the real photo', async () => {
    let resolve: (url: string) => void = () => undefined;
    const loadSource = vi.fn(() => new Promise<string>((done) => void (resolve = done)));
    render(<PhotoViewer photos={PHOTOS} index={0} loadSource={loadSource} onMove={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('img')).toHaveAttribute('src', 'https://s/thumb-a');
    expect(screen.getByText(/cargando la foto en su calidad real/)).toBeInTheDocument();
    resolve('https://s/real-a');

    await waitFor(() => expect(screen.getByRole('img')).toHaveAttribute('src', 'https://s/real-a'));
    expect(screen.getByText('IMG_0001.jpg')).toBeInTheDocument();
    expect(screen.queryByText(/cargando/)).not.toBeInTheDocument();
    expect(loadSource).toHaveBeenCalledWith('a');
  });

  it('says so, and keeps the small image, when the real photo cannot be had', async () => {
    render(<PhotoViewer photos={PHOTOS} index={0} loadSource={() => Promise.reject(new Error('no'))} onMove={vi.fn()} onClose={vi.fn()} />);

    expect(await screen.findByText(/no pudimos cargar la foto en su calidad real/)).toBeInTheDocument();
    expect(screen.getByRole('img')).toHaveAttribute('src', 'https://s/thumb-a');
  });

  it('moves between photos with the buttons and the arrow keys, asking for each real photo only when it is shown, and closes with Escape', async () => {
    const user = userEvent.setup();
    const onMove = vi.fn();
    const onClose = vi.fn();
    const loadSource = vi.fn((id: string) => Promise.resolve(`https://s/real-${id}`));
    const { rerender } = render(<PhotoViewer photos={PHOTOS} index={0} loadSource={loadSource} onMove={onMove} onClose={onClose} />);

    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Siguiente' }));
    expect(onMove).toHaveBeenCalledWith(1);
    rerender(<PhotoViewer photos={PHOTOS} index={1} loadSource={loadSource} onMove={onMove} onClose={onClose} />);

    await waitFor(() => expect(screen.getByRole('img')).toHaveAttribute('src', 'https://s/real-b'));
    expect(loadSource).toHaveBeenCalledTimes(2);
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeDisabled();
    await user.keyboard('{ArrowLeft}');
    expect(onMove).toHaveBeenLastCalledWith(0);
    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });

  it('shows nothing when the position is not a photo', () => {
    render(<PhotoViewer photos={PHOTOS} index={5} loadSource={() => Promise.resolve('x')} onMove={vi.fn()} onClose={vi.fn()} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
