import type { ReactElement } from 'react';

/**
 * Main page of the site. Its content is still to be defined: for now it welcomes the visitor and explains where a client's
 * booking is opened from. When the real content exists, this component is the only one to change.
 * @returns The page.
 */
export function HomePage(): ReactElement {
  return (
    <div className="stack">
      <header className="stack stack--tight">
        <h1>Bienvenido a PhotoSense Studio</h1>
        <p className="muted">Muy pronto vas a encontrar aquí todo sobre nuestro trabajo.</p>
      </header>

      <section className="card" aria-labelledby="booking-title">
        <h2 id="booking-title">¿Tienes una reserva?</h2>
        <p>Abre el enlace que tu fotógrafo te envió por WhatsApp para ver tu reserva, confirmar tus pagos y más.</p>
      </section>
    </div>
  );
}
