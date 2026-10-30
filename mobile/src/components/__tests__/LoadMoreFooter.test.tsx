import { fireEvent, render, screen } from '@testing-library/react-native';
import { LoadMoreFooter } from '../LoadMoreFooter';

describe('LoadMoreFooter', () => {
  it('shows nothing when there are no more pages', () => {
    render(<LoadMoreFooter hasMore={false} isLoading={false} failed={false} onLoadMore={jest.fn()} />);

    expect(screen.queryByText('Ver más')).toBeNull();
  });

  it('offers "Ver más" and loads the next page when it is tapped', () => {
    const onLoadMore = jest.fn();
    render(<LoadMoreFooter hasMore isLoading={false} failed={false} onLoadMore={onLoadMore} />);

    fireEvent.press(screen.getByText('Ver más'));

    expect(onLoadMore).toHaveBeenCalledTimes(1);
  });

  it('says it is loading and ignores taps meanwhile', () => {
    const onLoadMore = jest.fn();
    render(<LoadMoreFooter hasMore isLoading failed={false} onLoadMore={onLoadMore} />);

    fireEvent.press(screen.getByText('Cargando…'));

    expect(onLoadMore).not.toHaveBeenCalled();
  });

  it('says why and offers to try again when the page could not be loaded', () => {
    const onLoadMore = jest.fn();
    render(<LoadMoreFooter hasMore isLoading={false} failed onLoadMore={onLoadMore} />);

    expect(screen.getByText(/No se pudo cargar más/)).toBeTruthy();
    fireEvent.press(screen.getByText('Reintentar'));

    expect(onLoadMore).toHaveBeenCalledTimes(1);
  });
});
