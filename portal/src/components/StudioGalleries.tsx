import type { ReactElement } from 'react';
import { useStudioGalleries } from '../hooks/useStudioGalleries';
import { useStudioLogout } from '../hooks/useStudioMutations';
import type { StudioGalleriesProps } from '../types/components/StudioComponents.types';
import { describeLoadError } from '../utils/apiErrors';
import { LoadErrorScreen } from './LoadErrorScreen';
import { LoadingScreen } from './LoadingScreen';
import { StudioGalleryCard } from './StudioGalleryCard';

/**
 * The galleries of the signed-in photographer, the most recently created first, a page at a time, with the way out: signing out,
 * or signing out and forgetting this browser, which makes the next sign-in ask for the code again.
 * @param props Component props.
 * @returns The page.
 */
export function StudioGalleries({ user }: StudioGalleriesProps): ReactElement {
  const galleries = useStudioGalleries();
  const logout = useStudioLogout();
  const items = galleries.data?.pages.flatMap((page) => page.items) ?? [];

  let list: ReactElement;
  if (galleries.isPending) {
    list = <LoadingScreen message="Cargando tus galerías…" />;
  } else if (galleries.isError && items.length === 0) {
    list = <LoadErrorScreen view={describeLoadError(galleries.error)} onRetry={() => void galleries.refetch()} />;
  } else if (items.length === 0) {
    list = (
      <section className="card">
        <h2>Todavía no tienes galerías</h2>
        <p className="muted">Aparecen solas cuando completas una sesión en la app.</p>
      </section>
    );
  } else {
    list = (
      <div className="stack">
        {items.map((gallery) => (
          <StudioGalleryCard key={gallery.id} gallery={gallery} />
        ))}
        {galleries.hasNextPage && (
          <div className="stack stack--tight">
            {galleries.isFetchNextPageError && (
              <p className="field__error" role="alert">
                No se pudo cargar más. Revisa la conexión e inténtalo de nuevo.
              </p>
            )}
            <button type="button" className="button button--secondary" disabled={galleries.isFetchingNextPage} onClick={() => void galleries.fetchNextPage()}>
              {galleries.isFetchingNextPage ? 'Cargando…' : galleries.isFetchNextPageError ? 'Reintentar' : 'Ver más'}
            </button>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="stack">
      <header className="studio-bar">
        <div className="stack stack--tight">
          <h1>Tus galerías</h1>
          <p className="muted">Hola, {user.name}.</p>
        </div>
        <div className="studio-bar__actions">
          <button type="button" className="button button--secondary" disabled={logout.isPending} onClick={() => logout.mutate(false)}>
            Salir
          </button>
          <button type="button" className="button button--secondary" disabled={logout.isPending} onClick={() => logout.mutate(true)}>
            Salir y olvidar este navegador
          </button>
        </div>
      </header>
      {list}
    </div>
  );
}
