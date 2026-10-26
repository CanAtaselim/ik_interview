import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { useState } from 'react';
import { Role } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import { useRequestBlacklistEntry } from './queries';

const tomorrow = () => {
  const date = new Date();
  date.setDate(date.getDate() + 1);
  date.setHours(0, 0, 0, 0);
  return date;
};

/**
 * Adayı kara listeye alır. İK Uzmanının talebi İK Yöneticisinin onayına gider; İK Yöneticisinin
 * eklediği kayıt hemen yürürlüğe girer.
 * @param {{ candidate: { id: string, name: string }, onClose: () => void, onSaved?: () => void }} props
 */
export default function BlacklistRequestDialog({ candidate, onClose, onSaved }) {
  const toast = useToast();
  const { hasAnyRole } = useAuth();
  const requestEntry = useRequestBlacklistEntry();
  const [reasonId, setReasonId] = useState(null);
  const [description, setDescription] = useState('');
  const [expiresAt, setExpiresAt] = useState(null);
  const [errors, setErrors] = useState({});

  const approvesImmediately = hasAnyRole([Role.HrManager]);

  const submit = () => {
    const found = {};
    if (!reasonId) found.reasonId = 'Kara liste nedeni seçmelisiniz.';
    if (!description.trim()) found.description = 'Kara listeye alma gerekçesini yazmalısınız.';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    requestEntry.mutate(
      {
        candidateId: candidate.id,
        reasonId,
        description: description.trim(),
        expiresAt: expiresAt ? expiresAt.toISOString() : null,
      },
      {
        onSuccess: () => {
          toast.success(
            approvesImmediately
              ? 'Aday kara listeye alındı.'
              : 'Kara liste talebi İK Yöneticisinin onayına gönderildi.',
          );
          onSaved?.();
          onClose();
        },
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label={approvesImmediately ? 'Kara listeye al' : 'Talebi gönder'}
        severity="danger"
        loading={requestEntry.isPending}
        onClick={submit}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header="Kara listeye al"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <p className="form-grid__wide">
          <strong>{candidate.name}</strong> kara listeye alınacak.{' '}
          {approvesImmediately
            ? 'Kayıt hemen yürürlüğe girer.'
            : 'Talebiniz İK Yöneticisi onayladığında yürürlüğe girer.'}{' '}
          Kayıt ve gerekçesi yalnızca İK tarafından görülür.
        </p>

        <div className="form-grid__wide">
          <FormField label="Neden" htmlFor="blacklist-reason" error={errors.reasonId}>
            <DictionarySelect
              type="BlacklistReason"
              inputId="blacklist-reason"
              value={reasonId}
              invalid={Boolean(errors.reasonId)}
              onChange={setReasonId}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <FormField label="Gerekçe" htmlFor="blacklist-description" error={errors.description}>
            <InputTextarea
              id="blacklist-description"
              rows={4}
              autoResize
              maxLength={2000}
              placeholder="Ne oldu, ne zaman oldu?"
              value={description}
              invalid={Boolean(errors.description)}
              onChange={(event) => setDescription(event.target.value)}
            />
          </FormField>
        </div>

        <FormField label="Bitiş tarihi" htmlFor="blacklist-expires">
          <Calendar
            inputId="blacklist-expires"
            value={expiresAt}
            onChange={(event) => setExpiresAt(event.value ?? null)}
            minDate={tomorrow()}
            dateFormat="dd.mm.yy"
            placeholder="Boş bırakılırsa kalıcı"
            showIcon
            showButtonBar
          />
        </FormField>
      </div>
    </Dialog>
  );
}
