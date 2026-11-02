import { useState } from 'react';
import { goBack } from '../navigation/appNavigation';
import type { GalleryResponse } from '../types/api/gallery';
import type { GalleryTermsFormState } from '../types/hooks/useGalleryForms.types';
import { galleryErrorMessage } from '../utils/apiErrors';
import { validateTerms } from '../utils/gallery';
import { useGalleryCommand } from './useGalleryCommand';

/**
 * State of the form that changes the terms of a gallery: how many photos the package includes, how many are a gift and what an extra
 * costs. It starts with what the gallery has now.
 * @param gallery The gallery.
 * @returns The state of the form.
 */
export function useGalleryTermsForm(gallery: GalleryResponse): GalleryTermsFormState {
  const command = useGalleryCommand(gallery.id);
  const [included, setIncludedValue] = useState(String(gallery.includedPhotos));
  const [bonus, setBonusValue] = useState(String(gallery.bonusPhotos));
  const [price, setPriceValue] = useState(String(gallery.extraPhotoPrice.amount));
  const [validationError, setValidationError] = useState<string | null>(null);

  /**
   * Builds a setter that also clears the validation message.
   * @param set Setter of one field.
   * @returns The setter for the form.
   */
  const clearing = (set: (value: string) => void) => (value: string) => {
    set(value);
    setValidationError(null);
  };

  return {
    included,
    bonus,
    price,
    validationError,
    submitError: command.isError ? galleryErrorMessage(command.error) : null,
    isPending: command.isPending,
    setIncluded: clearing(setIncludedValue),
    setBonus: clearing(setBonusValue),
    setPrice: clearing(setPriceValue),
    submit: () => {
      const result = validateTerms(included, bonus, price, gallery.maxPhotos);
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      command.mutate(
        { kind: 'editTerms', terms: { includedPhotos: result.includedPhotos, bonusPhotos: result.bonusPhotos, extraPhotoPrice: result.extraPhotoPrice } },
        { onSuccess: goBack },
      );
    },
  };
}
