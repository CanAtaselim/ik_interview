import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { confirmDialog } from 'primereact/confirmdialog';
import { DataTable } from 'primereact/datatable';
import { Dropdown } from 'primereact/dropdown';
import { InputText } from 'primereact/inputtext';
import { useState } from 'react';
import EmptyState from '@/shared/components/EmptyState';
import FormField from '@/shared/components/FormField';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { ROLE_LABELS } from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import AdminNav from '../components/AdminNav';
import { useCreateRoleMapping, useDeleteRoleMapping, useRoleMappings } from '../queries';
import '../admin.css';

const ROLE_OPTIONS = Object.entries(ROLE_LABELS).map(([value, label]) => ({ value, label }));

/**
 * Active Directory grubu → uygulama rolü eşlemesi. Kullanıcı bir sonraki girişinde, üyesi olduğu
 * gruplara göre rollerini alır; çalışmakta olan oturumlar etkilenmez.
 */
export default function RoleMappingsPage() {
  const toast = useToast();
  const { data: mappings = [], isLoading } = useRoleMappings();
  const create = useCreateRoleMapping();
  const remove = useDeleteRoleMapping();
  const [groupName, setGroupName] = useState('');
  const [role, setRole] = useState(null);
  const [errors, setErrors] = useState({});

  const add = (event) => {
    event.preventDefault();
    const found = {};
    if (!groupName.trim()) found.groupName = 'AD grubunun adını yazmalısınız.';
    if (!role) found.role = 'Bir rol seçmelisiniz.';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    create.mutate(
      { groupName: groupName.trim(), role },
      {
        onSuccess: () => {
          setGroupName('');
          setRole(null);
          toast.success('Eşleme eklendi. Gruptakiler bir sonraki girişlerinde bu rolü alır.');
        },
        onError: (error) => {
          const fieldErrors = error.response?.data?.errors;
          if (fieldErrors)
            setErrors(
              Object.fromEntries(
                Object.entries(fieldErrors).map(([field, messages]) => [field, messages[0]]),
              ),
            );
          else toast.error(getErrorMessage(error));
        },
      },
    );
  };

  const confirmDelete = (mapping) =>
    confirmDialog({
      header: 'Eşleme silinsin mi?',
      message: `“${mapping.groupName}” grubundakiler bir sonraki girişlerinde “${ROLE_LABELS[mapping.role]}” rolünü kaybeder.`,
      acceptLabel: 'Sil',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        remove.mutate(mapping.id, {
          onSuccess: () => toast.success('Eşleme silindi.'),
          onError: (error) => toast.error(getErrorMessage(error)),
        }),
    });

  return (
    <>
      <AdminNav />
      <PageHeader
        title="Rol eşlemesi"
        subtitle="Hangi AD grubunun uygulamada hangi role karşılık geldiği."
      />
      <div className="detail-sections">
        <section className="card card--padded settings-card" aria-labelledby="mapping-add">
          <h2 id="mapping-add" className="section-title">
            Eşleme ekle
          </h2>
          <form className="inline-form" onSubmit={add} noValidate>
            <FormField label="AD grubu" htmlFor="mapping-group" error={errors.groupName}>
              <InputText
                id="mapping-group"
                value={groupName}
                maxLength={512}
                placeholder="Grubun adı veya tam DN'i"
                invalid={Boolean(errors.groupName)}
                onChange={(event) => {
                  setGroupName(event.target.value);
                  setErrors((current) => ({ ...current, groupName: undefined }));
                }}
              />
            </FormField>
            <FormField label="Rol" htmlFor="mapping-role" error={errors.role}>
              <Dropdown
                inputId="mapping-role"
                value={role}
                options={ROLE_OPTIONS}
                placeholder="Rol seçin"
                invalid={Boolean(errors.role)}
                onChange={(event) => {
                  setRole(event.value);
                  setErrors((current) => ({ ...current, role: undefined }));
                }}
                filter
                filterInputAutoFocus
              />
            </FormField>
            <Button type="submit" label="Ekle" icon="pi pi-plus" loading={create.isPending} />
          </form>
          <p className="muted">
            Bir grup birden çok role eşlenebilir; kullanıcı üyesi olduğu tüm grupların rollerini
            alır. Sistem Admin rolüne eşlenmiş son grup silinemez.
          </p>
        </section>

        <section className="card" aria-label="Rol eşlemeleri">
          {isLoading ? (
            <TableSkeleton rows={5} />
          ) : (
            <DataTable
              value={mappings}
              dataKey="id"
              emptyMessage={<EmptyState icon="pi pi-key" title="Henüz eşleme yok" />}
            >
              <Column header="Rol" body={(mapping) => ROLE_LABELS[mapping.role]} />
              <Column header="AD grubu" field="groupName" />
              <Column
                header=""
                className="col-actions"
                body={(mapping) => (
                  <Button
                    icon="pi pi-trash"
                    text
                    severity="secondary"
                    aria-label={`${mapping.groupName} eşlemesini sil`}
                    onClick={() => confirmDelete(mapping)}
                  />
                )}
              />
            </DataTable>
          )}
        </section>
      </div>
    </>
  );
}
