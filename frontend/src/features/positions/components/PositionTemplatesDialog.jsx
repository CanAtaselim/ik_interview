import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { MultiSelect } from 'primereact/multiselect';
import { useState } from 'react';
import { Link } from 'react-router';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { ROUTES } from '@/shared/constants/routes';
import {
  useInterviewTemplates,
  useSetPositionInterviewTemplates,
} from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';

/**
 * Pozisyonun mülakat şablonlarını seçtirir (ör. teknik ve final için ayrı). Mülakat planlanınca
 * tipine uyan şablon otomatik bağlanır.
 * @param {{ position: { id: string, departmentId: string, interviewTemplates: { id: string }[] },
 *   onClose: () => void }} props
 */
export default function PositionTemplatesDialog({ position, onClose }) {
  const toast = useToast();
  const { data: templates = [], isLoading } = useInterviewTemplates({ isActive: true });
  const save = useSetPositionInterviewTemplates();
  const [selected, setSelected] = useState(
    position.interviewTemplates.map((template) => template.id),
  );

  // Pozisyonun birimine ait şablonlar üstte.
  const options = [...templates]
    .sort(
      (a, b) =>
        Number(b.departmentId === position.departmentId) -
        Number(a.departmentId === position.departmentId),
    )
    .map((template) => ({
      value: template.id,
      label: [
        template.name,
        template.interviewTypeName ?? 'tüm tipler',
        template.departmentName,
      ].join(' · '),
    }));

  const submit = () =>
    save.mutate(
      { positionId: position.id, templateIds: selected },
      {
        onSuccess: () => {
          toast.success('Mülakat şablonları kaydedildi.');
          onClose();
        },
        onError: (error) =>
          toast.error(error.response?.data?.errors?.templateIds?.[0] ?? getErrorMessage(error)),
      },
    );

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button type="button" label="Kaydet" loading={save.isPending} onClick={submit} />
    </div>
  );

  return (
    <Dialog
      visible
      header="Mülakat şablonları"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <div className="form-grid__wide">
          <FormField
            label="Bu pozisyonun mülakatlarında kullanılacak şablonlar"
            htmlFor="position-templates"
          >
            <MultiSelect
              inputId="position-templates"
              value={selected}
              options={options}
              onChange={(event) => setSelected(event.value)}
              placeholder={isLoading ? 'Yükleniyor…' : 'Şablon seçin'}
              emptyMessage="Aktif şablon yok."
              display="chip"
              filter={options.length > 8}
              showSelectAll={false}
              disabled={isLoading}
            />
          </FormField>
        </div>
        <p className="muted form-grid__wide">
          Her mülakat tipi için bir şablon seçmeniz yeterli. Şablon yoksa{' '}
          <Link to={`${ROUTES.interviewTemplates}?selected=new`}>yeni şablon oluşturun</Link>.
        </p>
      </div>
    </Dialog>
  );
}
