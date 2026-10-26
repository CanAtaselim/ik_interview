import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { SelectButton } from 'primereact/selectbutton';
import { useState } from 'react';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import { useCloseApplication, useUndoApplicationChange } from './queries';

const OUTCOME_OPTIONS = [
  { label: 'Olumsuz (reddedildi)', value: 'Rejected' },
  { label: 'Aday çekildi', value: 'Withdrawn' },
];

/**
 * Adayın bir pozisyondaki sürecini sonlandırır: sonuç + neden (zorunlu) + not. İsteğe bağlı olarak
 * hemen ardından kara liste talebi açılır.
 * @param {{ application: { id: string, candidateName: string, positionTitle: string },
 *   onClose: () => void, onClosed: (options: { openBlacklistRequest: boolean }) => void }} props
 */
export default function CloseApplicationDialog({ application, onClose, onClosed }) {
  const toast = useToast();
  const closeApplication = useCloseApplication();
  const undo = useUndoApplicationChange();
  const [status, setStatus] = useState('Rejected');
  const [reasonId, setReasonId] = useState(null);
  const [note, setNote] = useState('');
  const [openBlacklistRequest, setOpenBlacklistRequest] = useState(false);
  const [reasonError, setReasonError] = useState(null);

  const submit = () => {
    if (!reasonId) {
      setReasonError('Ret nedeni seçmelisiniz.');
      return;
    }

    closeApplication.mutate(
      {
        applicationId: application.id,
        status,
        rejectionReasonId: reasonId,
        note: note.trim() || null,
      },
      {
        onSuccess: () => {
          toast.action(
            `${application.candidateName}: “${application.positionTitle}” süreci sonlandırıldı.`,
            {
              label: 'Geri al',
              onAction: () =>
                undo.mutate(application.id, {
                  onSuccess: () => toast.success('Süreç yeniden açıldı.'),
                  onError: (error) => toast.error(getErrorMessage(error)),
                }),
            },
          );
          onClosed({ openBlacklistRequest });
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
        label="Süreci sonlandır"
        severity="danger"
        loading={closeApplication.isPending}
        onClick={submit}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header="Süreci sonlandır"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <p className="form-grid__wide">
          <strong>{application.candidateName}</strong> adayının{' '}
          <strong>{application.positionTitle}</strong> pozisyonundaki süreci sonlanacak. Yanlışlıkla
          yaparsanız 10 dakika içinde geri alabilirsiniz.
        </p>

        <div className="form-grid__wide">
          <FormField label="Sonuç" htmlFor="close-status">
            <SelectButton
              id="close-status"
              value={status}
              options={OUTCOME_OPTIONS}
              allowEmpty={false}
              onChange={(event) => setStatus(event.value)}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <FormField label="Neden" htmlFor="close-reason" error={reasonError}>
            <DictionarySelect
              type="RejectionReason"
              inputId="close-reason"
              value={reasonId}
              invalid={Boolean(reasonError)}
              onChange={(id) => {
                setReasonId(id);
                setReasonError(null);
              }}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <FormField label="Not (isteğe bağlı)" htmlFor="close-note">
            <InputTextarea
              id="close-note"
              rows={3}
              autoResize
              maxLength={2000}
              value={note}
              onChange={(event) => setNote(event.target.value)}
            />
          </FormField>
        </div>

        <label className="toolbar__check form-grid__wide" htmlFor="close-blacklist">
          <Checkbox
            inputId="close-blacklist"
            checked={openBlacklistRequest}
            onChange={(event) => setOpenBlacklistRequest(event.checked)}
          />
          Ardından bu aday için kara liste talebi aç
        </label>
      </div>
    </Dialog>
  );
}
