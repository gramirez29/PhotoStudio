import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import type { UploadQueueRowProps } from '../types/components/UploadComponents.types';
import { describeUploadStatus } from '../utils/uploadMessages';
import { UploadProgressBar } from './UploadProgressBar';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * One photo of the queue of an upload: its name, where it is, the bar while it is sent, and why it failed when it did.
 * @param props Component props.
 * @returns The row.
 */
export function UploadQueueRow({ name, status, problem, progress }: UploadQueueRowProps): ReactElement {
  const styles = useStyles();

  return (
    <View style={styles.row}>
      <Text style={styles.name} numberOfLines={1}>
        {name}
      </Text>
      <Text style={status === 'failed' ? styles.failed : styles.status}>{describeUploadStatus(status)}</Text>
      {status === 'uploading' && <UploadProgressBar fraction={progress} accessibilityLabel={`Subiendo ${name}`} />}
      {problem !== null && <Text style={styles.failed}>{problem}</Text>}
    </View>
  );
}

/** Styles of the row. */
const useStyles = makeStyles((palette) => ({
  row: {
    gap: 2,
    paddingVertical: spacing.xs,
  },
  name: {
    color: palette.onSurface,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  status: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  failed: {
    color: palette.error,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
}));
