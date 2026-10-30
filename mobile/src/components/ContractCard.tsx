import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { spacing } from '../theme/tokens';
import type { ContractCardProps } from '../types/components/ContractCard.types';
import { describeContract } from '../utils/contract';
import { AppCard } from './AppCard';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Card of the booking detail with the contract: whether it is signed, by whom, how and when. When the client signed through the
 * link it also shows what they declared (identity number, people, place and what the photos may be used for), which the
 * photographer needs for the invoice and for planning the session.
 * @param props Component props.
 * @returns The card.
 */
export function ContractCard({ booking }: ContractCardProps): ReactElement {
  const styles = useStyles();
  const [headline, ...details] = describeContract(booking.contract);

  return (
    <AppCard style={styles.card}>
      <Text style={styles.title}>Contrato</Text>
      <Text style={styles.body}>{headline}</Text>
      {details.map((line) => (
        <Text key={line} style={styles.detail}>
          {line}
        </Text>
      ))}
    </AppCard>
  );
}

/** Styles of the card. */
const useStyles = makeStyles((palette) => ({
  card: {
    gap: spacing.xs,
    marginTop: spacing.md,
  },
  title: {
    color: palette.onSurface,
    fontSize: 16,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
  body: {
    color: palette.onSurface,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  detail: {
    color: palette.onSurfaceVariant,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
