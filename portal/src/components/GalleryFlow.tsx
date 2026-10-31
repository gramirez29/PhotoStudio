import { useCallback, useReducer, type ReactElement } from 'react';
import { useGallerySummary } from '../hooks/usePortalGallery';
import type { GalleryFlowProps } from '../types/components/GalleryComponents.types';
import { galleryFlowReducer, INITIAL_GALLERY_FLOW } from '../utils/galleryFlow';
import { describeGalleryCard } from '../utils/galleryCard';
import { CodeEntryStep } from './CodeEntryStep';
import { CodeRequestStep } from './CodeRequestStep';
import { GalleryChooser } from './GalleryChooser';

/** Message shown when the verification expires while the client is choosing. */
const SESSION_EXPIRED_NOTICE = 'Tu verificación venció. Confirma tu correo otra vez; las fotos que elegiste se conservan.';

/**
 * The gallery of a booking, at the same link: a card that says where the photos are, and the way to open them. Seeing the photos
 * asks for the code sent to the client's email, like any action that matters, because they are what the client has not paid for
 * yet. With no gallery for the client to see (the booking has none, or its proofs are not ready) it shows nothing at all.
 * @param props Component props.
 * @returns The current step, the card, or nothing.
 */
export function GalleryFlow({ token, session, onSessionChange }: GalleryFlowProps): ReactElement | null {
  const summary = useGallerySummary(token);
  const [state, dispatch] = useReducer(galleryFlowReducer, INITIAL_GALLERY_FLOW);

  // A stable function: the chooser depends on it in an effect that must not run again on every render.
  const expire = useCallback((): void => {
    onSessionChange(null);
    dispatch({ type: 'sessionExpired' });
  }, [onSessionChange]);

  switch (state.step) {
    case 'request':
      return (
        <CodeRequestStep
          token={token}
          purpose="ver tus fotos"
          notice={state.session === null && state.maskedEmail !== null ? SESSION_EXPIRED_NOTICE : null}
          onSent={(maskedEmail) => dispatch({ type: 'codeSent', maskedEmail })}
          onCancel={() => dispatch({ type: 'close' })}
        />
      );
    case 'enter':
      return (
        <CodeEntryStep
          token={token}
          maskedEmail={state.maskedEmail ?? ''}
          onVerified={(verified) => {
            onSessionChange(verified);
            dispatch({ type: 'verified', session: verified });
          }}
          onCancel={() => dispatch({ type: 'close' })}
        />
      );
    case 'choose':
      return state.session === null ? null : (
        <GalleryChooser token={token} session={state.session} onSessionExpired={expire} onClose={() => dispatch({ type: 'close' })} />
      );
    case 'closed':
      break;
  }

  const words = summary.data === undefined || summary.data === null ? null : describeGalleryCard(summary.data.status);
  if (words === null) {
    return null;
  }

  return (
    <section className="card" aria-labelledby="gallery-card-title">
      <h2 id="gallery-card-title">Tus fotos</h2>
      <p>{words.text}</p>
      {words.action !== null && (
        <div className="actions">
          <button type="button" className="button" onClick={() => dispatch({ type: 'open', now: Date.now(), session })}>
            {words.action}
          </button>
        </div>
      )}
    </section>
  );
}
