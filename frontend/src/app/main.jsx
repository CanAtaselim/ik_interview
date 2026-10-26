import { QueryClientProvider } from '@tanstack/react-query';
import { PrimeReactProvider } from 'primereact/api';
import { ConfirmDialog } from 'primereact/confirmdialog';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router';

import '@fontsource-variable/inter';
import 'primereact/resources/themes/lara-light-indigo/theme.css';
import 'primeicons/primeicons.css';
import '@/shared/styles/tokens.css';
import '@/shared/styles/theme-overrides.css';
import '@/shared/styles/global.css';

import { ToastProvider } from '@/shared/components/toast';
import { setupPrimeLocale } from './primeLocale';
import { queryClient } from './queryClient';
import { router } from './router';

setupPrimeLocale();

createRoot(document.getElementById('root')).render(
  <StrictMode>
    {/* Sağlayıcı yerel ayarı kendisi belirler; verilmezse 'en' olur ve setupPrimeLocale'in 'tr' seçimini ezer. */}
    <PrimeReactProvider value={{ locale: 'tr' }}>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <RouterProvider router={router} />
          {/* Onay pencereleri her yerden confirmDialog(...) ile açılır. */}
          <ConfirmDialog />
        </ToastProvider>
      </QueryClientProvider>
    </PrimeReactProvider>
  </StrictMode>,
);
