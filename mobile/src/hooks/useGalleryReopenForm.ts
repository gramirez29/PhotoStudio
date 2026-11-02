import { useState } from 'react';
import { GALLERY_REASON_MAX_LENGTH } from '../constants/gallery';
import { goBack } from '../navigation/appNavigation';
import type { GalleryResponse } from '../types/api/gallery';
import type { GalleryReopenFormState } from '../types/hooks/useGalleryForms.types';
import { galleryErrorMessage } from '../utils/apiErrors';
import { validateReopenReason } from '../utils/gallery';
import { useGalleryCommand } from './useGalleryCommand';

/**
 * State of the form that asks why the selection is reopened. The reason is required and stays in the photographer's record; the client
 * is told by email that they can choose again, without the reason.
 * @param gallery The gallery.
 * @returns The state of the form.
 */
export function useGalleryReopenForm(gallery: GalleryResponse): GalleryReopenFormState {
  const command = useGalleryCommand(gallery.id);
  const [text, setTextValue] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  return {
    text,
    validationError,
    submitError: command.isError ? galleryErrorMessage(command.error) : null,
    isPending: command.isPending,
    setText: (value: string) => {
      setTextValue(value);
      setValidationError(null);
    },
    submit: () => {
      const result = validateReopenReason(text, GALLERY_REASON_MAX_LENGTH);
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      command.mutate({ kind: 'reopenSelection', reason: result.reason }, { onSuccess: goBack });
    },
  };
}
