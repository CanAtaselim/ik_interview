import { ROUTES } from '@/shared/constants/routes';
import KpiCard from '../charts/KpiCard';
import { useDashboardSection } from '../queries';

/** Üst şerit: anlık sayılar ve dönem karşılaştırmalı göstergeler. @param {{ filters: import('../api').DashboardFilters }} props */
export default function SummaryStrip({ filters }) {
  const { data, isPending, isError, refetch } = useDashboardSection('summary', filters);

  if (isError)
    return (
      <p className="cell-sub" role="alert">
        Özet alınamadı.{' '}
        <button type="button" className="link-button" onClick={() => refetch()}>
          Yeniden dene
        </button>
      </p>
    );

  return (
    <div className="dashboard-kpis">
      <KpiCard
        label="Açık pozisyon"
        value={data?.openPositions}
        note={data ? `${data.openHeadcount} kişilik açık kadro` : undefined}
        isPending={isPending}
        to={ROUTES.positions}
      />
      <KpiCard
        label="Aktif süreç"
        value={data?.activeApplications}
        isPending={isPending}
        note="Süreci devam eden aday"
      />
      <KpiCard
        label="Bekleyen CV"
        value={data?.pendingCvReviews}
        note={data?.overdueCvReviews ? `${data.overdueCvReviews} tanesi gecikmiş` : 'Gecikmiş yok'}
        tone={data?.overdueCvReviews ? 'danger' : 'default'}
        isPending={isPending}
        to={ROUTES.cvReviews}
      />
      <KpiCard
        label="Planlı mülakat"
        value={data?.upcomingInterviews}
        note="Henüz yapılmamış"
        isPending={isPending}
        to={ROUTES.interviews}
      />
      <KpiCard
        label="Sonucu girilmemiş mülakat"
        value={data?.awaitingOutcome}
        tone={data?.awaitingOutcome ? 'danger' : 'default'}
        note="Saati geçmiş"
        isPending={isPending}
        to={ROUTES.interviews}
      />
      <KpiCard
        label="Yeni aday"
        value={data?.newCandidates.value}
        previous={data?.newCandidates.previous}
        isPending={isPending}
        to={ROUTES.candidates}
      />
      <KpiCard
        label="İşe alınan"
        value={data?.hires.value}
        previous={data?.hires.previous}
        isPending={isPending}
      />
      <KpiCard
        label="Tamamlanan mülakat"
        value={data?.interviewsHeld.value}
        previous={data?.interviewsHeld.previous}
        isPending={isPending}
      />
    </div>
  );
}
