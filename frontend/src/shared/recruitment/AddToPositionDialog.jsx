import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { Dropdown } from 'primereact/dropdown';
import { useState } from 'react';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import { useOpenPositions, useStartApplication } from './queries';

/**
 * Adayı açık bir pozisyonun sürecine ekler.
 * @param {{ candidate: { id: string, name: string }, onClose: () => void }} props
 */
export default function AddToPositionDialog({ candidate, onClose }) {
  const toast = useToast();
  const { data: positions = [], isLoading } = useOpenPositions();
  const startApplication = useStartApplication();
  const [positionId, setPositionId] = useState(null);

  const options = positions.map((position) => ({
    value: position.id,
    label: `${position.title} · ${position.departmentName}`,
  }));

  const submit = () =>
    startApplication.mutate(
      { candidateId: candidate.id, positionId },
      {
        onSuccess: () => {
          toast.success('Aday pozisyonun sürecine eklendi.');
          onClose();
        },
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label="Sürece ekle"
        disabled={!positionId}
        loading={startApplication.isPending}
        onClick={submit}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header="Pozisyona ekle"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <div className="form-grid__wide">
          <FormField
            label={`${candidate.name} hangi pozisyon için değerlendirilecek?`}
            htmlFor="add-position"
          >
            <Dropdown
              inputId="add-position"
              value={positionId}
              options={options}
              onChange={(event) => setPositionId(event.value)}
              placeholder={isLoading ? 'Yükleniyor…' : 'Açık pozisyon seçin'}
              emptyMessage="Açık pozisyon yok. Önce bir pozisyon açılmalı."
              disabled={isLoading}
              filter
              filterInputAutoFocus
            />
          </FormField>
        </div>
      </div>
    </Dialog>
  );
}
