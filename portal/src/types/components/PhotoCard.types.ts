/** A small label over the corner of the image, such as whether the photo already has its final. */
export interface PhotoCardBadge {
  /** What the label says. */
  readonly label: string;
  /** True for a label that warns of something missing, which takes the color of warnings. */
  readonly warning?: boolean;
}

/** The mark that lets the person pick the photo among many. */
export interface PhotoCardSelect {
  /** Whether the photo is picked. */
  readonly checked: boolean;
  /** Called with the new state. */
  readonly onChange: (checked: boolean) => void;
  /** What the mark is for, in words, for screen readers. */
  readonly label: string;
  /** True to keep it from changing while something that depends on it runs. */
  readonly disabled?: boolean;
}

/** The icon button of the footer that does something with the photo, such as downloading it. */
export interface PhotoCardAction {
  /** What it does, in words: it is the name of the button for screen readers and its tooltip. */
  readonly label: string;
  /** Called when it is pressed. */
  readonly onClick: () => void;
  /** True to keep it from being pressed while something runs. */
  readonly disabled?: boolean;
}

/** Props of the `PhotoCard` component. */
export interface PhotoCardProps {
  /** Address of the image to show, or null when there is none: the card shows an icon instead. */
  readonly image: string | null;
  /** Name of the photo, shown under the image. */
  readonly name: string;
  /** What is known about the photo, in short lines of words, shown one under the other under its name. */
  readonly details: readonly string[];
  /** What opening the photo is called, for screen readers. */
  readonly openLabel: string;
  /** Called when the image is pressed, to see the photo large. */
  readonly onOpen: () => void;
  /** A label over the corner of the image, if any. */
  readonly badge?: PhotoCardBadge | undefined;
  /** The mark to pick the photo, if it can be picked. */
  readonly select?: PhotoCardSelect | undefined;
  /** The icon button of the footer, if the photo has an action. */
  readonly action?: PhotoCardAction | undefined;
}
