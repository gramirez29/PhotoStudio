import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { Canvas, Path, Skia } from '@shopify/react-native-skia';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import type { StatusDonutProps } from '../types/components/home.types';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';

/** Side of the square the chart is drawn in, in points. */
const SIZE = 200;

/** Thickness of the ring, in points. */
const RING_WIDTH = 28;

/** Degrees left empty between two slices, so each one is told apart from its neighbors. */
const GAP_DEGREES = 3;

/** The angle the chart starts at: Skia counts from the right, the chart starts at the top. */
const TOP_OFFSET = -90;

/**
 * Builds the arc of a slice as a path on the ring.
 * @param startAngle Where the slice starts, in degrees from the top, clockwise.
 * @param sweepAngle How far it goes, in degrees.
 * @returns The path.
 */
function arc(startAngle: number, sweepAngle: number): ReturnType<typeof Skia.Path.Make> {
  const inset = RING_WIDTH / 2;
  const path = Skia.Path.Make();
  path.addArc(Skia.XYWHRect(inset, inset, SIZE - RING_WIDTH, SIZE - RING_WIDTH), startAngle + TOP_OFFSET, sweepAngle);
  return path;
}

/**
 * The donut chart of the bookings by status: a ring drawn with Skia, one slice per status that has bookings, in the colors of the
 * statuses, with the total in the middle. With no bookings it is an empty ring. The drawing is not read by a screen reader; the
 * description of the chart is.
 * @param props Component props.
 * @returns The chart.
 */
export function StatusDonut({ segments, total, accessibilityLabel }: StatusDonutProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const single = segments.length === 1;

  return (
    <View accessible accessibilityRole="image" accessibilityLabel={accessibilityLabel} style={styles.container}>
      <Canvas style={styles.canvas}>
        <Path path={arc(0, 359.99)} style="stroke" strokeWidth={RING_WIDTH} color={palette.surface} />
        {segments.length === 0 && <Path path={arc(0, 359.99)} style="stroke" strokeWidth={RING_WIDTH} color={palette.outlineVariant} />}
        {segments.map((segment) => (
          <Path
            key={segment.status}
            path={arc(segment.startAngle + (single ? 0 : GAP_DEGREES / 2), Math.min(359.99, segment.sweepAngle - (single ? 0 : GAP_DEGREES)))}
            style="stroke"
            strokeWidth={RING_WIDTH}
            color={palette[STATUS_COLOR_ROLES[segment.status]]}
          />
        ))}
      </Canvas>
      <View style={styles.center} pointerEvents="none">
        <Text style={styles.total}>{total}</Text>
        <Text style={styles.caption}>{total === 1 ? 'cita' : 'citas'}</Text>
      </View>
    </View>
  );
}

/** Styles of the chart: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  container: {
    alignSelf: 'center',
    height: SIZE,
    width: SIZE,
  },
  canvas: {
    height: SIZE,
    width: SIZE,
  },
  center: {
    alignItems: 'center',
    bottom: 0,
    justifyContent: 'center',
    left: 0,
    position: 'absolute',
    right: 0,
    top: 0,
  },
  total: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 36,
  },
  caption: {
    color: palette.onSurfaceVariant,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 14,
  },
}));
