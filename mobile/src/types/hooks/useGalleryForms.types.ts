/** State of the form that changes the terms of a gallery. */
export interface GalleryTermsFormState {
  /** Photos the package includes, as typed. */
  readonly included: string;
  /** Photos given on top, as typed. */
  readonly bonus: string;
  /** Price of each extra photo, as typed. */
  readonly price: string;
  /** What is wrong with the values, in words; null when they are fine. */
  readonly validationError: string | null;
  /** Why the request failed, in words; null when it did not. */
  readonly submitError: string | null;
  /** True while the request is running. */
  readonly isPending: boolean;
  /** Changes the included photos. */
  setIncluded(value: string): void;
  /** Changes the gift photos. */
  setBonus(value: string): void;
  /** Changes the price of an extra photo. */
  setPrice(value: string): void;
  /** Validates and sends. */
  submit(): void;
}

/** State of the form that asks why the selection is reopened. */
export interface GalleryReopenFormState {
  /** The reason, as typed. */
  readonly text: string;
  /** What is wrong with it, in words; null when it is fine. */
  readonly validationError: string | null;
  /** Why the request failed, in words; null when it did not. */
  readonly submitError: string | null;
  /** True while the request is running. */
  readonly isPending: boolean;
  /** Changes the reason. */
  setText(value: string): void;
  /** Validates and sends. */
  submit(): void;
}
