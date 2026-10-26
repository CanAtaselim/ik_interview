import { Button } from 'primereact/button';
import { Link } from 'react-router';
import EmptyState from '@/shared/components/EmptyState';
import StatusTag from '@/shared/components/StatusTag';
import { useToast } from '@/shared/components/toast';
import { APPLICATION_STATUS } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { useUndoApplicationChange } from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate, formatDaysSince } from '@/shared/utils/format';

/**
 * Adayın pozisyonlardaki süreçleri. Aday aynı anda birden çok pozisyonda süreçte olabilir.
 * @param {{ applications: object[], onAdd: () => void, onClose: (application: object) => void }} props
 */
export default function ApplicationsTab({ applications, onAdd, onClose }) {
  const toast = useToast();
  const undo = useUndoApplicationChange();

  const addButton = <Button label="Pozisyona ekle" icon="pi pi-plus" onClick={onAdd} />;

  if (applications.length === 0) {
    return (
      <EmptyState
        icon="pi pi-briefcase"
        title="Aday henüz bir pozisyon için değerlendirilmiyor"
        description="Adayı açık bir pozisyona ekleyin; süreç ilk aşamadan başlar."
        action={addButton}
      />
    );
  }

  return (
    <div className="screenings">
      <div className="screenings__actions">{addButton}</div>
      <ul className="application-list">
        {applications.map((application) => {
          const isActive = application.status === 'Active';
          return (
            <li key={application.id} className="application-list__item">
              <div className="application-list__main">
                <div className="dialog-title">
                  <Link to={ROUTES.pipeline(application.positionId)} className="table-link">
                    {application.positionTitle}
                  </Link>
                  <StatusTag value={application.status} map={APPLICATION_STATUS} />
                </div>
                <p className="cell-sub">
                  {isActive
                    ? `${application.stageName} aşamasında · ${formatDaysSince(application.stageEnteredAt).toLocaleLowerCase('tr')}`
                    : `${application.stageName} aşamasında sonlandı · ${formatDate(application.closedAt)}`}
                  {' · '}Başlangıç: {formatDate(application.startedAt)}
                </p>
                {application.closingReason && (
                  <p>
                    {application.closingReason}
                    {application.closingNote && (
                      <span className="muted"> — {application.closingNote}</span>
                    )}
                  </p>
                )}
              </div>
              {application.canUndo && (
                <Button
                  label="Geri al"
                  icon="pi pi-undo"
                  text
                  disabled={undo.isPending}
                  onClick={() =>
                    undo.mutate(application.id, {
                      onSuccess: () => toast.success('Son değişiklik geri alındı.'),
                      onError: (error) => toast.error(getErrorMessage(error)),
                    })
                  }
                />
              )}
              {isActive && (
                <Button
                  label="Süreci sonlandır"
                  severity="danger"
                  outlined
                  onClick={() => onClose(application)}
                />
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
