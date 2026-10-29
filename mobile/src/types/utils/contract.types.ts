/** One line of the contract text, ready to draw. */
export interface ContractParagraph {
  /** Text of the line, without its leading spaces. */
  readonly text: string;
  /** Whether the line is indented (the options of the image use, for example). */
  readonly indented: boolean;
}

/** The contract text split into what the screen draws: the title and the paragraphs below it. */
export interface ContractLayout {
  /** First line of the contract, or null when the text is empty. */
  readonly title: string | null;
  /** The other non-blank lines, in order. */
  readonly paragraphs: readonly ContractParagraph[];
}
