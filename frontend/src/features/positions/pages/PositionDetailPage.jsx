import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import DetailList from '@/shared/components/DetailList';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { POSITION_STATUS } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate } from '@/shared/utils/format';
import PositionFormDialog from '../components/PositionFormDialog';
import PositionTemplatesDialog from '../components/PositionTemplatesDialog';
import { useChangePositionStatus, usePosition } from '../queries';

// Her durumdan yapılabilecek geçişler; sonucu açıklayan onay metniyle birlikte.
const STATUS_ACTIONS = {
  Open: [
    {
      status: 'OnHold',
      label: 'Beklemeye al',
      message:
        'Beklemedeki pozisyona yeni aday eklenemez. İstediğiniz zaman yeniden açabilirsiniz.',
    },
    {
      status: 'Closed',
      label: 'Kapat',
      message: 'Pozisyon kapatılacak ve yeni aday eklenemeyecek. Gerekirse yeniden açabilirsiniz.',
    },
    {
      status: 'Cancelled',
      label: 'İptal et',
      danger: true,
      message: 'İptal edilen pozisyon yeniden açılamaz.',
    },
  ],
  OnHold: [
    { status: 'Open', label: 'Yeniden aç', message: 'Pozisyon yeniden aday kabul edecek.' },
    {
      status: 'Closed',
      label: 'Kapat',
      message: 'Pozisyon kapatılacak ve yeni aday eklenemeyecek. Gerekirse yeniden açabilirsiniz.',
    },
    {
      status: 'Cancelled',
      label: 'İptal et',
      danger: true,
      message: 'İptal edilen pozisyon yeniden açılamaz.',
    },
  ],
  Closed: [{ status: 'Open', label: 'Yeniden aç', message: 'Pozisyon yeniden aday kabul edecek.' }],
  Cancelled: [],
};

export default function PositionDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const { data: position, isLoading, isError, error } = usePosition(id);
  const changeStatus = useChangePositionStatus();
  const [editing, setEditing] = useState(false);
  const [editingTemplates, setEditingTemplates] = useState(false);

  if (isLoading) return <TableSkeleton rows={6} />;
  if (isError) {
    // Görme yetkisi olmayan pozisyon da "bulunamadı" olarak döner.
    return error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(error)}</p>
    );
  }

  const confirmStatusChange = (action) =>
    confirmDialog({
      header: `${action.label}: ${position.title}`,
      message: action.message,
      acceptLabel: action.label,
      rejectLabel: 'Vazgeç',
      acceptClassName: action.danger ? 'p-button-danger' : undefined,
      accept: () =>
        changeStatus.mutate(
          { id: position.id, status: action.status },
          {
            onSuccess: () => toast.success('Pozisyonun durumu güncellendi.'),
            onError: (mutationError) => toast.error(getErrorMessage(mutationError)),
          },
        ),
    });

  const actions = position.canManage && (
    <>
      <Link
        to={ROUTES.pipeline(position.id)}
        className="p-button p-component p-button-outlined link-button"
      >
        <span className="p-button-icon p-button-icon-left pi pi-sitemap" aria-hidden="true" />
        <span className="p-button-label">Süreç hattı</span>
      </Link>
      {STATUS_ACTIONS[position.status].map((action) => (
        <Button
          key={action.status}
          label={action.label}
          outlined={!action.danger}
          text={action.danger}
          severity={action.danger ? 'danger' : undefined}
          disabled={changeStatus.isPending}
          onClick={() => confirmStatusChange(action)}
        />
      ))}
      {position.status !== 'Cancelled' && (
        <Button label="Düzenle" icon="pi pi-pencil" onClick={() => setEditing(true)} />
      )}
    </>
  );

  return (
    <>
      <Link to={ROUTES.positions} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> Pozisyonlar
      </Link>
      <PageHeader
        title={
          <span className="dialog-title">
            {position.title}
            <StatusTag value={position.status} map={POSITION_STATUS} />
          </span>
        }
        subtitle={`${position.departmentName} · ${position.levelName}`}
        actions={
          <>
            <Link
              to={ROUTES.comparison(position.id)}
              className="p-button p-component p-button-outlined link-button"
            >
              <span
                className="p-button-icon p-button-icon-left pi pi-chart-bar"
                aria-hidden="true"
              />
              <span className="p-button-label">Adayları karşılaştır</span>
            </Link>
            {actions}
          </>
        }
      />

      <div className="detail-sections">
        <section className="card card--padded">
          <DetailList
            items={[
              { label: 'Doluluk', value: `${position.filledCount} / ${position.headcount}` },
              { label: 'Sorumlu İK', value: position.responsibleHrName ?? 'Atanmadı' },
              { label: 'Açılış tarihi', value: formatDate(position.createdAt) },
              {
                label: 'Kaynak',
                value: position.hiringRequestId ? (
                  <Link to={ROUTES.hiringRequest(position.hiringRequestId)}>Kadro talebini aç</Link>
                ) : (
                  'İK tarafından açıldı'
                ),
              },
            ]}
          />
        </section>

        <section className="card card--padded" aria-labelledby="position-templates-title">
          <div className="section-heading">
            <h2 id="position-templates-title" className="section-title">
              Mülakat şablonları
            </h2>
            {position.canManage && (
              <Button
                label="Düzenle"
                icon="pi pi-pencil"
                text
                onClick={() => setEditingTemplates(true)}
              />
            )}
          </div>
          {position.interviewTemplates.length === 0 ? (
            <p className="muted">
              Şablon bağlanmadı. Bağlanırsa mülakat planlanırken tipine uyan şablon otomatik gelir
              ve mülakatçı soruları mülakat sayfasında görür.
            </p>
          ) : (
            <ul className="plain-list">
              {position.interviewTemplates.map((template) => (
                <li key={template.id}>
                  <strong>{template.name}</strong>
                  <span className="cell-sub">
                    {' '}
                    · {template.interviewTypeName ?? 'tüm mülakat tipleri'}
                    {!template.isActive && ' · pasif'}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="card card--padded">
          <h2 className="section-title">İş tanımı</h2>
          {position.jobDescription ? (
            <p className="multiline">{position.jobDescription}</p>
          ) : (
            <p className="muted">
              İş tanımı henüz yazılmadı.
              {position.canManage && ' “Düzenle” ile ekleyebilirsiniz.'}
            </p>
          )}
        </section>
      </div>

      {editingTemplates && (
        <PositionTemplatesDialog position={position} onClose={() => setEditingTemplates(false)} />
      )}
      {editing && (
        <PositionFormDialog
          position={position}
          onClose={() => setEditing(false)}
          onSaved={() => setEditing(false)}
        />
      )}
    </>
  );
}
