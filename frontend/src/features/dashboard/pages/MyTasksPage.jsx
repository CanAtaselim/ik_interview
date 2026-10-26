import { Tag } from 'primereact/tag';
import { Link } from 'react-router';
import { useHiringRequests } from '@/features/hiring-requests/queries';
import { HR_ROLES, INTERVIEWER_ROLES, Role } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { CV_REVIEW_DECISION, URGENCY } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { useCvReviews, useInterviews } from '@/shared/recruitment/queries';
import { formatDate, formatTimeRange } from '@/shared/utils/format';
import '../dashboard.css';

const APPROVER_ROLES = [Role.DepartmentManager, Role.HrManager];
const MAX_ITEMS = 10;
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
 * Ana sayfa: kullanıcıdan aksiyon bekleyen işler. Her öğe tek tıkla ilgili ekrana götürür.
 * İş üreten modüller (mülakat, değerlendirme) eklendikçe yeni bölümler buraya gelecek.
 */
export default function MyTasksPage() {
  const { user, hasAnyRole } = useAuth();
  const firstName = user.displayName.split(' ')[0];

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

  // Mülakat listeleri sayfalanmaz; diğer bölümlerle aynı biçime getirilir.
  const asPage = (items) => items && { items: items.slice(0, MAX_ITEMS), totalCount: items.length };
  const upcomingPage = asPage(upcoming.data?.filter((item) => !item.awaitingOutcome));
  const awaitingPage = asPage(awaitingOutcome.data);
  const toScorePage = asPage(toScore.data);

  const sections = [cvReviews, overdueReviews, toScore, upcoming, awaitingOutcome, approvals];
  const isLoading = sections.some((section) => section.isLoading);
  const isEmpty = [
    cvReviews.data,
    overdueReviews.data,
    toScorePage,
    upcomingPage,
    awaitingPage,
    approvals.data,
  ].every((page) => (page?.items ?? []).length === 0);

  return (
    <>
      <PageHeader
        title="Bekleyen işlerim"
        subtitle={`Merhaba ${firstName}, sizden aksiyon bekleyen işler burada listelenir.`}
      />

      {isLoading && (
        <section className="card">
          <TableSkeleton rows={3} />
        </section>
      )}

      {!isLoading && isEmpty && (
        <section className="card">
          <EmptyState
            icon="pi pi-check-circle"
            title="Şu anda bekleyen işiniz yok"
            description="İncelenecek CV, puanlanacak mülakat veya onay bekleyen talep olduğunda burada göreceksiniz."
          />
        </section>
      )}

      <div className="task-sections">
        <TaskSection
          id="to-score"
          title="Puanlanacak mülakatlarım"
          unit="mülakat"
          page={toScorePage}
          moreLink={ROUTES.interviews}
          renderItem={(interview) => (
            <InterviewItem interview={interview} to={ROUTES.scorecard(interview.id)} />
          )}
        />
        <TaskSection
          id="upcoming-interviews"
          title="Yaklaşan mülakatlarım"
          unit="mülakat"
          severity="info"
          page={upcomingPage}
          moreLink={ROUTES.interviews}
          renderItem={(interview) => <InterviewItem interview={interview} />}
        />
        <TaskSection
          id="awaiting-outcome"
          title="Sonucu girilmemiş mülakatlar"
          unit="mülakat"
          page={awaitingPage}
          moreLink={ROUTES.interviews}
          renderItem={(interview) => <InterviewItem interview={interview} />}
        />
        <TaskSection
          id="cv-reviews"
          title="İncelemenizi bekleyen CV'ler"
          unit="CV"
          page={cvReviews.data}
          moreLink={ROUTES.cvReviews}
          renderItem={(review) => (
            <ReviewItem
              review={review}
              detail={`${review.suggestedByName} önerdi · Son tarih: ${formatDate(review.dueAt)}`}
            />
          )}
        />
        <TaskSection
          id="overdue-reviews"
          title="Geciken CV değerlendirmeleri"
          unit="değerlendirme"
          severity="danger"
          page={overdueReviews.data}
          moreLink={`${ROUTES.cvReviews}?status=Overdue`}
          renderItem={(review) => (
            <ReviewItem
              review={review}
              detail={`${review.reviewerName} bekleniyor · Son tarih: ${formatDate(review.dueAt)}`}
            />
          )}
        />
        <TaskSection
          id="approvals"
          title="Onayınızı bekleyen kadro talepleri"
          unit="talep"
          page={approvals.data}
          moreLink={`${ROUTES.hiringRequests}?awaiting=1`}
          renderItem={(request) => (
            <Link to={ROUTES.hiringRequest(request.id)} className="task-list__item">
              <span className="task-list__main">
                <span className="task-list__title">{request.title}</span>
                <span className="cell-sub">
                  {request.departmentName} · {request.levelName} · {request.headcount} kişi ·{' '}
                  {request.requestedByName} · {formatDate(request.createdAt)}
                </span>
              </span>
              <StatusTag value={request.urgency} map={URGENCY} />
              <i className="pi pi-angle-right" aria-hidden="true" />
            </Link>
          )}
        />
      </div>
    </>
  );
}

/**
 * Bir iş türünün listesi; öğe yoksa hiç görünmez.
 * @param {{ id: string, title: string, unit: string, severity?: string,
 *   page: { items: object[], totalCount: number } | undefined, moreLink: string,
 *   renderItem: (item: object) => import('react').ReactNode }} props
 */
function TaskSection({ id, title, unit, severity = 'warning', page, moreLink, renderItem }) {
  const items = page?.items ?? [];
  if (items.length === 0) return null;

  const titleId = `${id}-title`;

  return (
    <section className="card" aria-labelledby={titleId}>
      <header className="task-section__header">
        <h2 id={titleId} className="section-title">
          {title}
        </h2>
        <Tag
          value={page.totalCount}
          severity={severity}
          rounded
          aria-label={`${page.totalCount} ${unit}`}
        />
      </header>
      <ul className="task-list">
        {items.map((item) => (
          <li key={item.id}>{renderItem(item)}</li>
        ))}
      </ul>
      {page.totalCount > items.length && (
        <Link to={moreLink} className="task-section__more">
          Tümünü gör ({page.totalCount})
        </Link>
      )}
    </section>
  );
}

function ReviewItem({ review, detail }) {
  return (
    <Link to={ROUTES.cvReview(review.id)} className="task-list__item">
      <span className="task-list__main">
        <span className="task-list__title">{review.candidateName}</span>
        <span className="cell-sub">
          {review.positionTitle ?? 'Pozisyon belirtilmeden'} · {detail}
        </span>
      </span>
      <StatusTag value={review.decision} map={CV_REVIEW_DECISION} />
      <i className="pi pi-angle-right" aria-hidden="true" />
    </Link>
  );
}

function InterviewItem({ interview, to = ROUTES.interview(interview.id) }) {
  return (
    <Link to={to} className="task-list__item">
      <span className="task-list__main">
        <span className="task-list__title">{interview.candidateName}</span>
        <span className="cell-sub">
          {formatTimeRange(interview.startsAt, interview.endsAt)} · {interview.typeName} ·{' '}
          {interview.positionTitle}
        </span>
      </span>
      <i className="pi pi-angle-right" aria-hidden="true" />
    </Link>
  );
}
