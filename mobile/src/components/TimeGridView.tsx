import { useCallback, useMemo, useRef, useState, type ReactElement } from 'react';
import { ActivityIndicator, FlatList, View, type LayoutChangeEvent, type NativeScrollEvent, type NativeSyntheticEvent } from 'react-native';
import { useAgendaStore } from '../agenda/agendaStore';
import { GRID_PAGES_EACH_WAY } from '../constants/agenda';
import { useAgendaRanges } from '../hooks/useAgendaRanges';
import { useAppTheme } from '../hooks/useAppTheme';
import { openBooking } from '../navigation/appNavigation';
import { makeStyles } from '../theme/makeStyles';
import type { TimeGridViewProps } from '../types/components/agenda.types';
import { rangeContains, shiftAnchor, visibleRange } from '../utils/agenda/ranges';
import { gridPageDays, initialGridOffset } from '../utils/agenda/timeGrid';
import { Placeholder } from './Placeholder';
import { TimeGridPage } from './TimeGridPage';

/** Every page the photographer can reach, as its distance from the page the grid opened on. */
const PAGE_OFFSETS = Array.from({ length: GRID_PAGES_EACH_WAY * 2 + 1 }, (_, index) => index - GRID_PAGES_EACH_WAY);

/**
 * The grid of hours of the agenda, like the day, three-day and week views of Google Calendar: one page per period, which the
 * photographer swipes sideways, with the bookings as blocks as tall as the session lasts, in rows of half an hour. It opens on the day
 * it is given, scrolled to the current time when that is today. The page on screen and the ones next to it are loaded, so swiping
 * shows bookings at once; the title of the header follows the page.
 * @param props Component props.
 * @returns The grid, or a spinner or a message while there is nothing to show.
 */
export function TimeGridView({ view, anchor, includeEnded }: TimeGridViewProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const setVisibleDate = useAgendaStore((state) => state.setVisibleDate);
  const [width, setWidth] = useState(0);
  const [page, setPage] = useState(0);
  const anchorTime = anchor.getTime();
  const origin = useMemo(() => new Date(anchorTime), [anchorTime]);

  const ranges = useMemo(
    () => [-1, 0, 1].map((step) => visibleRange(view, shiftAnchor(view, origin, page + step))),
    [view, origin, page],
  );
  const state = useAgendaRanges(ranges, includeEnded);

  // The first page decides how far the hours are scrolled; after that every page appears where the photographer left them.
  const [firstOffset] = useState(() => initialGridOffset(rangeContains(visibleRange(view, origin), new Date()), new Date()));
  const scrollOffset = useRef(firstOffset);

  const onLayout = (event: LayoutChangeEvent): void => setWidth(event.nativeEvent.layout.width);

  const onMomentumEnd = (event: NativeSyntheticEvent<NativeScrollEvent>): void => {
    if (width === 0) {
      return;
    }

    const next = Math.round(event.nativeEvent.contentOffset.x / width) - GRID_PAGES_EACH_WAY;
    if (next !== page) {
      setPage(next);
      setVisibleDate(gridPageDays(view, origin, next)[0] ?? origin);
    }
  };

  const onScrollOffset = useCallback((offset: number) => {
    scrollOffset.current = offset;
  }, []);
  const getOffset = useCallback(() => scrollOffset.current, []);

  if (state.isError) {
    return (
      <Placeholder message="No se pudo cargar la agenda. Revisa la conexión e inténtalo de nuevo." onRetry={() => void state.refetch()} />
    );
  }

  return (
    <View testID="time-grid" style={styles.container} onLayout={onLayout}>
      {width > 0 && (
        <FlatList
          accessibilityLabel="Agenda por horas"
          data={PAGE_OFFSETS}
          horizontal
          pagingEnabled
          showsHorizontalScrollIndicator={false}
          keyExtractor={(offset) => String(offset)}
          initialScrollIndex={GRID_PAGES_EACH_WAY}
          getItemLayout={(_, index) => ({ length: width, offset: width * index, index })}
          initialNumToRender={1}
          windowSize={3}
          maxToRenderPerBatch={1}
          onMomentumScrollEnd={onMomentumEnd}
          renderItem={({ item }) => (
            <TimeGridPage
              days={gridPageDays(view, origin, item)}
              bookings={state.bookings}
              width={width}
              getOffset={getOffset}
              onScrollOffset={onScrollOffset}
              onOpenBooking={openBooking}
            />
          )}
        />
      )}
      {state.isPending && (
        <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando la agenda" style={styles.loader} pointerEvents="none" />
      )}
    </View>
  );
}

/** Styles of the grid. */
const useStyles = makeStyles(() => ({
  container: {
    flex: 1,
  },
  loader: {
    position: 'absolute',
    alignSelf: 'center',
    top: 80,
  },
}));
