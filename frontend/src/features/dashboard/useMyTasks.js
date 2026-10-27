import { useHiringRequests } from '@/features/hiring-requests/queries';
import { HR_ROLES, INTERVIEWER_ROLES, Role } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import { useCvReviews, useInterviews } from '@/shared/recruitment/queries';

const APPROVER_ROLES = [Role.DepartmentManager, Role.HrManager];
export const MAX_ITEMS = 10;
const UPCOMING_DAYS = 7;

// Sorgu anahtarı her render'da değişmesin diye saat başına yuvarlanır.
const upcomingRange = () => {
  const from = new Date();
  from.setMinutes(0, 0, 0);
  const to = new Date(from);
  to.setDate(to.getDate() + UPCOMING_DAYS);
  return { from: from.toISOString(), to: to.toISOString() };
};

/**
 * Kullanıcıdan aksiyon bekleyen işlerin sorguları. "Bekleyen işlerim" sayfası ve menüdeki sayaç aynı
 * sorguları paylaşır (TanStack Query önbelleği tek istek atar). Rolü olmayan kullanıcı için ilgili
 * sorgu çalışmaz.
 *
 * `count`: aksiyon bekleyen iş sayısı. Bilgi amaçlı "yaklaşan mülakatlar" sayıya girmez.
 */
export function useMyTasks() {
  const { hasAnyRole } = useAuth();

  const cvReviews = useCvReviews(
    { status: 'Waiting', mine: true, pageSize: MAX_ITEMS },
    { enabled: hasAnyRole([Role.TeamLead]) },
  );
  const overdueReviews = useCvReviews(
    { status: 'Overdue', pageSize: MAX_ITEMS },
    { enabled: hasAnyRole(HR_ROLES) },
  );
  const upcoming = useInterviews(
    { ...upcomingRange(), mine: true, status: 'Scheduled' },
    { enabled: hasAnyRole(INTERVIEWER_ROLES) },
  );
  const awaitingOutcome = useInterviews(
    { awaitingOutcome: true },
    { enabled: hasAnyRole(HR_ROLES) },
  );
  const toScore = useInterviews(
    { awaitingMyScorecard: true },
    { enabled: hasAnyRole(INTERVIEWER_ROLES) },
  );
  const approvals = useHiringRequests(
    { awaitingMyDecision: true, pageSize: MAX_ITEMS },
    { enabled: hasAnyRole(APPROVER_ROLES) },
  );

  const count =
    (cvReviews.data?.totalCount ?? 0) +
    (overdueReviews.data?.totalCount ?? 0) +
    (approvals.data?.totalCount ?? 0) +
    (toScore.data?.length ?? 0) +
    (awaitingOutcome.data?.length ?? 0);

  return { cvReviews, overdueReviews, upcoming, awaitingOutcome, toScore, approvals, count };
}
