import type { ReactElement } from 'react';
import type { ContractDocumentProps } from '../types/components/ContractDocument.types';

/**
 * The text of the contract, laid out as the document it is: the title, then one paragraph per line, with the lines that start
 * with spaces (the options of the image use) indented. It scrolls inside its own box and can take the keyboard focus, so a
 * person who does not use a mouse can read it all.
 * @param props Component props.
 * @returns The document.
 */
export function ContractDocument({ text }: ContractDocumentProps): ReactElement {
  const lines = text.split('\n').filter((line) => line.trim().length > 0);
  const [title, ...body] = lines;

  return (
    <div className="contract" role="region" aria-label="Texto del contrato" tabIndex={0}>
      {title !== undefined && <p className="contract__title">{title}</p>}
      {body.map((line, index) => (
        <p key={`${index}-${line.slice(0, 24)}`} className={/^\s/.test(line) ? 'contract__option' : undefined}>
          {line.trim()}
        </p>
      ))}
    </div>
  );
}
