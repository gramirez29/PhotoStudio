import { useCallback, useMemo, useRef, useState, type ReactElement } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Text,
  type NativeScrollEvent,
  type NativeSyntheticEvent,
  type ViewabilityConfig,
  type ViewToken,
} from 'react-native';
import { useAgendaStore } from '../agenda/agendaStore';
import { SCHEDULE_INITIAL_CHUNKS_AFTER, SCHEDULE_MAX_CHUNKS_EACH_WAY } from '../constants/agenda';
import { useAgendaRanges } from '../hooks/useAgendaRanges';
import { useAppTheme } from '../hooks/useAppTheme';
import { useBottomInset } from '../hooks/useBottomInset';
import { useToday } from '../hooks/useToday';
import { openBooking } from '../navigation/appNavigation';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { ScheduleViewProps } from '../types/components/agenda.types';
import type { ScheduleRow } from '../types/utils/agenda.types';
import { shiftAnchor, visibleRange } from '../utils/agenda/ranges';
import { groupByDay } from '../utils/agenda/schedule';
import { buildScheduleRows, pageToLoad } from '../utils/agenda/scheduleRows';
import { Placeholder } from './Placeholder';
import { ScheduleDayRow } from './ScheduleDayRow';
import { ScheduleMonthBanner } from './ScheduleMonthBanner';

/** Room left at the end of the list so the floating button never hides the last booking. */
const LIST_END_SPACE = 96;

/** How much of a row has to be on screen to count as the one the photographer is looking at. */
const VIEWABILITY: ViewabilityConfig = { itemVisiblePercentThreshold: 60 };

/**
 * The list view of the agenda, like the "Schedule" view of Google Calendar: the days that have bookings, one under the other, each
 * with its weekday and number and its bookings as blocks of the color of their status, with a banner at each change of month, and
 * always today. It opens on the day it is given with the next 90 days already loaded, and loads the next or the previous 30 days as
 * the photographer scrolls toward an end, keeping what is on screen where it is; a spinner at that end says it is loading. The title of the header follows the month of the day at the
 * top. Pulling down reloads. Tapping a booking opens it.
 * @param props Component props.
 * @returns The list, or a spinner or a message while there is nothing to show.
 */
export function ScheduleView({ anchor, includeEnded }: ScheduleViewProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const bottomInset = useBottomInset();
  const today = useToday();
  const setVisibleDate = useAgendaStore((state) => state.setVisibleDate);
  const [startOffset, setStartOffset] = useState(0);
  const [endOffset, setEndOffset] = useState(SCHEDULE_INITIAL_CHUNKS_AFTER);
  const [refreshing, setRefreshing] = useState(false);
  const dragged = useRef(false);
  const anchorTime = anchor.getTime();

  const ranges = useMemo(
    () =>
      Array.from({ length: endOffset - startOffset + 1 }, (_, index) =>
        visibleRange('schedule', shiftAnchor('schedule', new Date(anchorTime), startOffset + index)),
      ),
    [anchorTime, startOffset, endOffset],
  );
  const state = useAgendaRanges(ranges, includeEnded);

  const rows = useMemo(() => {
    const first = ranges[0];
    const last = ranges[ranges.length - 1];
    if (first === undefined || last === undefined) {
      return [];
    }

    return buildScheduleRows(groupByDay(state.bookings, { from: first.from, to: last.to }, today), today);
  }, [ranges, state.bookings, today]);

  const canLoadBefore = startOffset > -SCHEDULE_MAX_CHUNKS_EACH_WAY;
  const canLoadAfter = endOffset < SCHEDULE_MAX_CHUNKS_EACH_WAY;
  const loadBefore = useCallback(() => {
    if (canLoadBefore && !state.isFetching) {
      setStartOffset((offset) => offset - 1);
    }
  }, [canLoadBefore, state.isFetching]);
  const loadAfter = useCallback(() => {
    if (canLoadAfter && !state.isFetching) {
      setEndOffset((offset) => offset + 1);
    }
  }, [canLoadAfter, state.isFetching]);

  const handleScroll = (event: NativeSyntheticEvent<NativeScrollEvent>): void => {
    // Nothing loads until the photographer has scrolled by hand: a list that opens near an end must not load without being asked.
    if (!dragged.current) {
      return;
    }

    const { contentOffset, layoutMeasurement, contentSize } = event.nativeEvent;
    const side = pageToLoad({ offset: contentOffset.y, viewport: layoutMeasurement.height, content: contentSize.height });
    if (side === 'end') {
      loadAfter();
    } else if (side === 'start') {
      loadBefore();
    }
  };

  // React Native needs this function to keep its identity for as long as the list lives; the setter of the store never changes.
  const onViewableItemsChanged = useCallback(
    ({ viewableItems }: { readonly viewableItems: ViewToken<ScheduleRow>[] }) => {
      const top = viewableItems.find((token) => token.item !== undefined);
      if (top?.item !== undefined) {
        setVisibleDate(top.item.date);
      }
    },
    [setVisibleDate],
  );

  const refresh = (): void => {
    setRefreshing(true);
    void state.refetch().finally(() => setRefreshing(false));
  };

  if (state.isPending) {
    return <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando la agenda" style={styles.loader} />;
  }

  if (state.isError) {
    return (
      <Placeholder message="No se pudo cargar la agenda. Revisa la conexión e inténtalo de nuevo." onRetry={() => void state.refetch()} />
    );
  }

  return (
    <FlatList
      accessibilityLabel="Agenda de reservas"
      data={rows}
      keyExtractor={(row) => row.key}
      renderItem={({ item }) =>
        item.kind === 'month' ? (
          <ScheduleMonthBanner label={item.label} />
        ) : (
          <ScheduleDayRow section={item.section} onOpenBooking={openBooking} />
        )
      }
      ListHeaderComponent={
        <>
          {state.isFetching && startOffset < 0 && (
            <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando reservas anteriores" style={styles.edgeLoader} />
          )}
          {state.truncated && (
            <Text style={styles.notice}>
              Hay más reservas de las que se pueden mostrar en este período. Busca las que faltan en Todas las reservas.
            </Text>
          )}
        </>
      }
      ListFooterComponent={
        state.isFetching && endOffset > SCHEDULE_INITIAL_CHUNKS_AFTER ? (
          <ActivityIndicator color={palette.primary} accessibilityLabel="Cargando reservas posteriores" style={styles.edgeLoader} />
        ) : null
      }
      onScrollBeginDrag={() => {
        dragged.current = true;
      }}
      onScroll={handleScroll}
      scrollEventThrottle={100}
      onViewableItemsChanged={onViewableItemsChanged}
      viewabilityConfig={VIEWABILITY}
      maintainVisibleContentPosition={{ minIndexForVisible: 0 }}
      refreshing={refreshing}
      onRefresh={refresh}
      contentContainerStyle={[styles.container, { paddingBottom: LIST_END_SPACE + bottomInset }]}
    />
  );
}

/** Styles of the list: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  container: {
    paddingHorizontal: spacing.md,
  },
  loader: {
    marginTop: spacing.lg,
  },
  edgeLoader: {
    marginVertical: spacing.md,
  },
  notice: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 13,
    marginVertical: spacing.sm,
  },
}));
