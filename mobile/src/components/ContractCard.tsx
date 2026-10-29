import type { ReactElement } from 'react';
import { StyleSheet, Text } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import type { ContractCardProps } from '../types/components/ContractCard.types';
import { describeContract } from '../utils/contract';
import { AppCard } from './AppCard';

/**
 * Card of the booking detail with the contract: whether it is signed, by whom, how and when. When the client signed through the
 * link it also shows what they declared (identity number, people, place and what the photos may be used for), which the
 * photographer needs for the invoice and for planning the session.
 * @param props Component props.
 * @returns The card.
 */
export function ContractCard({ booking }: ContractCardProps): ReactElement {
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
const styles = StyleSheet.create({
  card: {
    gap: spacing.xs,
    marginTop: spacing.md,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '700',
  },
  body: {
    color: colors.textPrimary,
    fontSize: 15,
  },
  detail: {
    color: colors.textSecondary,
    fontSize: 14,
  },
});
