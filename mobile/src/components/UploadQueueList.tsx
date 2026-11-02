import type { ReactElement } from 'react';
import { Text } from 'react-native';
import type { UploadQueueListProps } from '../types/components/UploadComponents.types';
import { ActionButton } from './ActionButton';
import { UploadQueueRow } from './UploadQueueRow';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * The queue of an upload: where each photo is, why a photo failed, and what to do about the ones that failed. While photos are being
 * sent the app has to stay open, and it says so. It shows nothing when the queue is empty.
 * @param props Component props.
 * @returns The queue, or nothing.
 */
export function UploadQueueList({ queue }: UploadQueueListProps): ReactElement | null {
  const styles = useStyles();
  const failed = queue.items.filter((item) => item.status === 'failed').length;

  if (queue.items.length === 0 && queue.problem === null) {
    return null;
  }

  return (
    <>
      {queue.running && <Text style={styles.hint}>Subiendo… deja la app abierta hasta que termine.</Text>}
      {queue.problem !== null && <Text style={styles.problem}>{queue.problem}</Text>}
      {queue.items.map((item) => (
        <UploadQueueRow key={item.id} name={item.file.name} status={item.status} problem={item.problem} progress={item.progress} />
      ))}
      {!queue.running && failed > 0 && (
        <>
          <ActionButton label={failed === 1 ? 'Reintentar la que falló' : `Reintentar las ${failed} que fallaron`} onPress={queue.retry} />
          <ActionButton label="Quitar de la lista las que fallaron" variant="danger" onPress={queue.dismissFailed} />
        </>
      )}
    </>
  );
}

/** Styles of the queue. */
const useStyles = makeStyles((palette) => ({
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  problem: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
