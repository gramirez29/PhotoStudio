import type { ReactElement } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { ContractTextProps } from '../types/components/ContractText.types';
import { splitContractText } from '../utils/contract';

/**
 * The text of the contract as the client reads it on the phone: the title and then one paragraph per line, in its own box that
 * scrolls, so a long contract does not push the form out of reach.
 * @param props Component props.
 * @returns The contract.
 */
export function ContractText({ text }: ContractTextProps): ReactElement {
  const layout = splitContractText(text);

  return (
    <View style={styles.box} accessibilityLabel="Texto del contrato">
      <ScrollView nestedScrollEnabled contentContainerStyle={styles.content}>
        {layout.title !== null && <Text style={styles.title}>{layout.title}</Text>}
        {layout.paragraphs.map((paragraph, index) => (
          <Text key={`${index}-${paragraph.text.slice(0, 24)}`} style={[styles.paragraph, paragraph.indented && styles.indented]}>
            {paragraph.text}
          </Text>
        ))}
      </ScrollView>
    </View>
  );
}

/** Styles of the contract box. */
const styles = StyleSheet.create({
  box: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    maxHeight: 360,
  },
  content: {
    gap: spacing.sm,
    padding: spacing.md,
  },
  title: {
    color: colors.textPrimary,
    fontSize: 15,
    fontWeight: '700',
  },
  paragraph: {
    color: colors.textPrimary,
    fontSize: 14,
    lineHeight: 20,
  },
  indented: {
    paddingLeft: spacing.md,
  },
});
