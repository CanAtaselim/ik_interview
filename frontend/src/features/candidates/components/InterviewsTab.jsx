import { Button } from 'primereact/button';
import { Link } from 'react-router';
import EmptyState from '@/shared/components/EmptyState';
import StatusTag from '@/shared/components/StatusTag';
import { INTERVIEW_MODE_LABELS, INTERVIEW_STATUS } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { formatTimeRange } from '@/shared/utils/format';

/**
 * Adayın mülakatları (tüm süreçlerinden), en yakın tarihli başta.
 * @param {{ interviews: import('@/shared/api/interviews').Interview[], canSchedule: boolean,
 *   onSchedule: () => void }} props
 */
export default function InterviewsTab({ interviews, canSchedule, onSchedule }) {
  const scheduleButton = canSchedule && (
    <Button label="Mülakat planla" icon="pi pi-calendar-plus" onClick={onSchedule} />
  );

  if (interviews.length === 0) {
    return (
      <EmptyState
        icon="pi pi-calendar"
        title="Henüz mülakat planlanmadı"
        description={
          canSchedule
            ? 'Adayın devam eden süreci için mülakat planlayın; mülakatçılar takvimlerinde görür.'
            : 'Mülakat planlamak için adayı önce bir pozisyonun sürecine ekleyin.'
        }
        action={scheduleButton}
      />
    );
  }

  const sorted = [...interviews].sort((a, b) => new Date(b.startsAt) - new Date(a.startsAt));

  return (
    <div className="screenings">
      {scheduleButton && <div className="screenings__actions">{scheduleButton}</div>}
      <ul className="application-list">
        {sorted.map((interview) => (
          <li key={interview.id} className="application-list__item">
            <div className="application-list__main">
              <div className="dialog-title">
                <Link to={ROUTES.interview(interview.id)} className="table-link">
                  {interview.typeName} mülakatı
                </Link>
                <StatusTag value={interview.status} map={INTERVIEW_STATUS} />
              </div>
              <p className="cell-sub">
                {[
                  formatTimeRange(interview.startsAt, interview.endsAt),
                  interview.positionTitle,
                  interview.roomName ?? INTERVIEW_MODE_LABELS[interview.mode],
                  interview.interviewers.map((item) => item.displayName).join(', '),
                ].join(' · ')}
              </p>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
