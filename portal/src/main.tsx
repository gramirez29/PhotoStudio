import '@fontsource-variable/dm-sans';
import '@fontsource-variable/josefin-sans';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './theme/tokens.css';
import './theme/global.css';
import './theme/booking.css';
import './theme/forms.css';
import './theme/contract.css';
import './theme/studio.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('The page has no #root element.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
