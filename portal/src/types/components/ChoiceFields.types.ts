import type { ReactNode } from 'react';

/** One option of a `RadioGroupField`. */
export interface RadioOption<Value extends string> {
  /** Value the option stands for. */
  readonly value: Value;
  /** Text the client reads. */
  readonly label: string;
}

/** Props of the `RadioGroupField` component. */
export interface RadioGroupFieldProps<Value extends string> {
  /** Identifier of the group; ties the legend and the error to it. */
  readonly id: string;
  /** Question the options answer. */
  readonly legend: string;
  /** Options, in the order they are shown. */
  readonly options: readonly RadioOption<Value>[];
  /** The option chosen, or null while none is. */
  readonly value: Value | null;
  /** Called with the value of the option the client picks. */
  readonly onChange: (value: Value) => void;
  /** Validation message; the group is marked invalid while there is one. */
  readonly error?: string | undefined;
}

/** Props of the `CheckboxField` component. */
export interface CheckboxFieldProps {
  /** Identifier of the checkbox; ties the label and the error to it. */
  readonly id: string;
  /** Text next to the box. */
  readonly children: ReactNode;
  /** Whether the box is ticked. */
  readonly checked: boolean;
  /** Called with the new state. */
  readonly onChange: (checked: boolean) => void;
  /** Validation message; the box is marked invalid while there is one. */
  readonly error?: string | undefined;
}
