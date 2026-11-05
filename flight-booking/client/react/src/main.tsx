import React from 'react';
import ReactDOM from 'react-dom/client';
// EJ2 Tailwind3 theme — required for TextBox / Button to render with
// proper borders, focus ring, hover, and primary-button colors.
import '@syncfusion/ej2-tailwind3-theme/styles/customized/tailwind3.css';
import App from './App';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
