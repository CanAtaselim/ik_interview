import { Button } from 'primereact/button';
import { Dropdown } from 'primereact/dropdown';
import { useState } from 'react';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { ROLE_LABELS } from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import AdminNav from '../components/AdminNav';
import { useApprovalFlow, useUpdateApprovalFlow } from '../queries';
import '../admin.css';

/**
 * Kadro talebinin onay akışı: hangi rollerin hangi sırayla onay vereceği. Değişiklik yalnızca
 * bundan sonra onaya gönderilen talepleri etkiler; onay bekleyenler gönderildikleri akışla devam eder.
 */
export default function ApprovalFlowPage() {
  const { data, isLoading } = useApprovalFlow();
  if (isLoading || !data) return <TableSkeleton rows={3} />;

  return (
    <>
      <AdminNav />
      <PageHeader
        title="Onay akışı"
        subtitle="Kadro taleplerinin kimler tarafından, hangi sırayla onaylanacağı."
      />
      <FlowEditor key={data.roles.join()} flow={data} />
    </>
  );
}

function FlowEditor({ flow }) {
  const toast = useToast();
  const update = useUpdateApprovalFlow();
  const [roles, setRoles] = useState(flow.roles);
  const [adding, setAdding] = useState(null);
  const available = flow.allowedRoles.filter((role) => !roles.includes(role));
  const dirty = roles.join() !== flow.roles.join();

  const move = (index, offset) =>
    setRoles((current) => {
      const next = [...current];
      [next[index], next[index + offset]] = [next[index + offset], next[index]];
      return next;
    });

  const save = () =>
    update.mutate(roles, {
      onSuccess: () =>
        toast.success('Onay akışı kaydedildi. Bundan sonra gönderilen talepler bu akışı izler.'),
      onError: (error) => toast.error(getErrorMessage(error)),
    });

  return (
    <section className="card card--padded settings-card" aria-labelledby="flow-title">
      <h2 id="flow-title" className="section-title">
        Kadro talebi onayı
      </h2>
      <ol className="ordered-list">
        {roles.map((role, index) => (
          <li key={role} className="ordered-list__item">
            <span className="ordered-list__main">
              <strong>{ROLE_LABELS[role]}</strong>
              <span className="cell-sub">
                {role === 'DepartmentManager'
                  ? ' · talebin biriminin yöneticisi'
                  : ' · tüm İK Yöneticileri'}
              </span>
            </span>
            <Button
              icon="pi pi-arrow-up"
              text
              severity="secondary"
              aria-label={`${ROLE_LABELS[role]} adımını yukarı taşı`}
              disabled={index === 0}
              onClick={() => move(index, -1)}
            />
            <Button
              icon="pi pi-arrow-down"
              text
              severity="secondary"
              aria-label={`${ROLE_LABELS[role]} adımını aşağı taşı`}
              disabled={index === roles.length - 1}
              onClick={() => move(index, 1)}
            />
            <Button
              icon="pi pi-times"
              text
              severity="secondary"
              aria-label={`${ROLE_LABELS[role]} adımını kaldır`}
              disabled={roles.length === 1}
              title={roles.length === 1 ? 'Akışta en az bir adım olmalı' : undefined}
              onClick={() => setRoles((current) => current.filter((item) => item !== role))}
            />
          </li>
        ))}
      </ol>

      {available.length > 0 && (
        <div className="inline-form">
          <Dropdown
            inputId="flow-add-role"
            aria-label="Eklenecek onaycı"
            value={adding}
            options={available.map((role) => ({ value: role, label: ROLE_LABELS[role] }))}
            onChange={(event) => setAdding(event.value)}
            placeholder="Onaycı ekle"
            filter
            filterInputAutoFocus
          />
          <Button
            type="button"
            label="Adım ekle"
            icon="pi pi-plus"
            outlined
            disabled={!adding}
            onClick={() => {
              setRoles((current) => [...current, adding]);
              setAdding(null);
            }}
          />
        </div>
      )}

      <p className="muted">
        Yalnızca Departman Yöneticisi ve İK Yöneticisi onay verebilir. Onay bekleyen talepler
        gönderildikleri akışla devam eder.
      </p>
      <div className="dialog-actions">
        <Button
          type="button"
          label="Kaydet"
          disabled={!dirty}
          loading={update.isPending}
          onClick={save}
        />
      </div>
    </section>
  );
}
