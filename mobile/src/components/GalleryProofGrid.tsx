import type { ReactElement } from 'react';
import { Image, Pressable, View } from 'react-native';
import type { GalleryProofGridProps } from '../types/components/GalleryComponents.types';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';

/**
 * The proofs of a gallery in a grid of small images. When removing is allowed, pressing and holding a proof asks to remove it.
 * @param props Component props.
 * @returns The grid.
 */
export function GalleryProofGrid({ proofs, onRemove }: GalleryProofGridProps): ReactElement {
  const styles = useStyles();

  return (
    <View style={styles.grid}>
      {proofs.map((proof) => (
        <Pressable
          key={proof.id}
          accessibilityRole="image"
          accessibilityLabel={proof.name}
          accessibilityHint={onRemove === undefined ? undefined : 'Mantén presionado para quitar esta prueba'}
          onLongPress={onRemove === undefined ? undefined : () => onRemove(proof.id)}
          style={styles.cell}
        >
          <Image source={{ uri: proof.url }} style={styles.image} resizeMode="cover" accessibilityIgnoresInvertColors />
        </Pressable>
      ))}
    </View>
  );
}

/** Styles of the grid. */
const useStyles = makeStyles((palette) => ({
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.xs,
  },
  cell: {
    aspectRatio: 1,
    backgroundColor: palette.surfaceVariant,
    borderRadius: 6,
    overflow: 'hidden',
    width: '32%',
  },
  image: {
    height: '100%',
    width: '100%',
  },
}));
