import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { useState } from 'react';
import FormField from '@/shared/components/FormField';

/**
 * İK Yöneticisinin, gönderilmemiş mülakat değerlendirmelerine rağmen adayı sonraki aşamaya
 * geçirmesi. Gerekçe zorunludur ve adayın geçmişine yazılır.
 * @param {{ candidateName: string, stageName: string, loading: boolean,
 *   onConfirm: (reason: string) => void, onClose: () => void }} props
 */
export default function BypassDialog({ candidateName, stageName, loading, onConfirm, onClose }) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState(null);

  const submit = () => {
    if (!reason.trim()) {
      setError('Gerekçe yazmalısınız.');
      return;
    }
    onConfirm(reason.trim());
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button type="button" label="Geçir" severity="danger" loading={loading} onClick={submit} />
    </div>
  );

  return (
    <Dialog
      visible
      header="Değerlendirmeler beklenmeden geçir"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <p className="form-grid__wide">
          <strong>{candidateName}</strong>, gönderilmemiş mülakat değerlendirmelerine rağmen{' '}
          <strong>{stageName}</strong> aşamasına geçirilecek. Gerekçe adayın geçmişine ve denetim
          kaydına yazılır.
        </p>
        <div className="form-grid__wide">
          <FormField label="Gerekçe" htmlFor="bypass-reason" error={error}>
            <InputTextarea
              id="bypass-reason"
              rows={3}
              autoResize
              maxLength={2000}
              value={reason}
              invalid={Boolean(error)}
              onChange={(event) => {
                setReason(event.target.value);
                setError(null);
              }}
            />
          </FormField>
        </div>
      </div>
    </Dialog>
  );
}
