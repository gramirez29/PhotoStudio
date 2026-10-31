import type { StudioGallery, StudioUser } from '../api/studio';
import type { PendingCode } from '../utils/studio.types';

/** Props of the `StudioPage` component. */
export interface StudioPageProps {
  /** Identifier of the gallery named by the address, or nothing for the list of galleries. */
  readonly galleryId?: string;
}

/** Props of the `StudioLoginForm` component. */
export interface StudioLoginFormProps {
  /** Called when the password was right in a browser that is not remembered: a code went to the email. */
  readonly onCodeRequired: (pending: PendingCode) => void;
}

/** Props of the `StudioCodeForm` component. */
export interface StudioCodeFormProps {
  /** Address the code went to, partly hidden. */
  readonly maskedEmail: string;
  /** Called when the person gives up and goes back to the password. */
  readonly onCancel: () => void;
}

/** Props of the `StudioGalleries` component. */
export interface StudioGalleriesProps {
  /** The photographer who is signed in. */
  readonly user: StudioUser;
}

/** Props of the `StudioGalleryCard` component. */
export interface StudioGalleryCardProps {
  /** The gallery to show. */
  readonly gallery: StudioGallery;
}
