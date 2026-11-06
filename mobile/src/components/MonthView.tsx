import { useMemo, useState, type ReactElement } from 'react';
import { ActivityIndicator, FlatList, View, type LayoutChangeEvent, type NativeScrollEvent, type NativeSyntheticEvent } from 'react-native';
import { changeAgendaView } from '../agenda/agendaManager';
import { useAgendaStore } from '../agenda/agendaStore';
import { MONTH_PAGES_EACH_WAY } from '../constants/agenda';
import { useAgendaRanges } from '../hooks/useAgendaRanges';
import { useAppTheme } from '../hooks/useAppTheme';
import { makeStyles } from '../theme/makeStyles';
import type { MonthViewProps } from '../types/components/agenda.types';
import { shiftAnchor, visibleRange } from '../utils/agenda/ranges';
import { Placeholder } from './Placeholder';
import { MonthPage } from './MonthPage';

/** Every month the photographer can reach, as its distance from the month the view opened on. */
const PAGE_OFFSETS = Array.from({ length: MONTH_PAGES_EACH_WAY * 2 + 1 }, (_, index) => index - MONTH_PAGES_EACH_WAY);

/**
 * The month view of the agenda, like the one of Google Calendar: a grid of weeks per month, which the photographer swipes sideways,
 * with each day showing its bookings as bars of the color of their status. The month on screen and the ones next to it are loaded.
 * Tapping a day opens that day in the day view. The title of the header follows the month.
 * @param props Component props.
 * @returns The view, or a message when the bookings could not be loaded.
 */
export function MonthView({ anchor, includeEnded }: MonthViewProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const setVisibleDate = useAgendaStore((state) => state.setVisibleDate);
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [page, setPage] = useState(0);
  const anchorTime = anchor.getTime();
  const origin = useMemo(() => new Date(anchorTime), [anchorTime]);

  const ranges = useMemo(() => [-1, 0, 1].map((step) => visibleRange('month', shiftAnchor('month', origin, page + step))), [origin, page]);
  const state = useAgendaRanges(ranges, includeEnded);

  const onLayout = (event: LayoutChangeEvent): void => {
    const { width, height } = event.nativeEvent.layout;
    setSize({ width, height });
  };

  const onMomentumEnd = (event: NativeSyntheticEvent<NativeScrollEvent>): void => {
    if (size.width === 0) {
      return;
    }

    const next = Math.round(event.nativeEvent.contentOffset.x / size.width) - MONTH_PAGES_EACH_WAY;
    if (next !== page) {
      setPage(next);
      setVisibleDate(shiftAnchor('month', origin, next));
    }
  };

  const openDay = (day: Date): void => {
    useAgendaStore.getState().setVisibleDate(day);
    void changeAgendaView('day');
  };

  if (state.isError) {
    return (
      <Placeholder message="No se pudo cargar la agenda. Revisa la conexión e inténtalo de nuevo." onRetry={() => void state.refetch()} />
    );
  }

  return (
    <View testID="month-view" style={styles.container} onLayout={onLayout}>
      {size.width > 0 && (
        <FlatList
          accessibilityLabel="Agenda por meses"
          data={PAGE_OFFSETS}
          horizontal
          pagingEnabled
          showsHorizontalScrollIndicator={false}
          keyExtractor={(offset) => String(offset)}
          initialScrollIndex={MONTH_PAGES_EACH_WAY}
          getItemLayout={(_, index) => ({ length: size.width, offset: size.width * index, index })}
          initialNumToRender={1}
          windowSize={3}
          maxToRenderPerBatch={1}
          onMomentumScrollEnd={onMomentumEnd}
          renderItem={({ item }) => (
            <MonthPage
              month={shiftAnchor('month', origin, item)}
              bookings={state.bookings}
              width={size.width}
              height={size.height}
              onOpenDay={openDay}
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

/** Styles of the view. */
const useStyles = makeStyles(() => ({
  container: {
    flex: 1,
  },
  loader: {
    alignSelf: 'center',
    position: 'absolute',
    top: 80,
  },
}));
