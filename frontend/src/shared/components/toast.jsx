import { Button } from 'primereact/button';
import { Toast } from 'primereact/toast';
import { createContext, useContext, useMemo, useRef } from 'react';

const ToastContext = createContext(null);

/** Uygulama genelinde kısa bildirimler: `const toast = useToast(); toast.success('…')`. */
export function ToastProvider({ children }) {
  const toast = useRef(null);

  const api = useMemo(
    () => ({
      /** @param {string} detail */
      success: (detail) =>
        toast.current?.show({ severity: 'success', summary: 'Tamam', detail, life: 3500 }),
      /** @param {string} detail */
      error: (detail) =>
        toast.current?.show({ severity: 'error', summary: 'Yapılamadı', detail, life: 7000 }),
      /**
       * Yapılan işi bildirir ve 10 saniye boyunca tek tıkla geri alma (veya başka bir işlem) sunar.
       * @param {string} detail @param {{ label: string, onAction: () => void }} action
       */
      action: (detail, { label, onAction }) =>
        toast.current?.show({
          severity: 'success',
          life: 10_000,
          content: ({ onClose }) => (
            <div className="toast-action">
              <span>{detail}</span>
              <Button
                type="button"
                label={label}
                outlined
                onClick={(event) => {
                  onAction();
                  onClose(event);
                }}
              />
            </div>
          ),
        }),
    }),
    [],
  );

  return (
    <ToastContext.Provider value={api}>
      {/* Sağ altta: üstteki kullanıcı menüsünü ve sayfa butonlarını kapatmaz. */}
      <Toast ref={toast} position="bottom-right" />
      {children}
    </ToastContext.Provider>
  );
}

/**
 * @returns {{ success: (detail: string) => void, error: (detail: string) => void,
 *   action: (detail: string, action: { label: string, onAction: () => void }) => void }}
 */
export function useToast() {
  return useContext(ToastContext);
}
