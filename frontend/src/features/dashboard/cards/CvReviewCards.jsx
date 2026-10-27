import { ROUTES } from '@/shared/constants/routes';
import BarChart from '../charts/BarChart';
import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import { formatDays } from '../charts/chartTheme';
import DoughnutChart from '../charts/DoughnutChart';
import KpiCard from '../charts/KpiCard';
import { useDashboardSection } from '../queries';

// Bekliyor, olumlu, olumsuz, beklemeye alındı (sunucudaki sıra).
const DECISION_COLORS = [
  '--color-warning',
  '--color-success',
  '--color-danger',
  '--color-border-strong',
];

/** CV değerlendirme: karar dağılımı, süre, SLA uyumu, gecikenler. @param {{ filters: import('../api').DashboardFilters }} props */
export default function CvReviewCards({ filters }) {
  const query = useDashboardSection('cv-reviews', filters);
  const { data } = query;

  const decisions = data?.decisions ?? [];
  const reviewers = data?.pendingByReviewer ?? [];

  return (
    <>
      <h2 className="dashboard-heading">CV değerlendirme</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="Ortalama karar süresi"
          value={data?.averageDecisionDays}
          previous={data?.previousAverageDecisionDays}
          lowerIsBetter
          format={formatDays}
          note="Öneriden karara"
          isPending={query.isPending}
        />
        <KpiCard
          label="Süresinde karar"
          value={
            data?.slaCompliancePercent === null || data === undefined
              ? null
              : `%${data.slaCompliancePercent}`
          }
          note="Son tarihine uyan karar oranı"
          isPending={query.isPending}
        />
        <KpiCard
          label="Geciken değerlendirme"
          value={data?.overdue}
          tone={data?.overdue ? 'danger' : 'default'}
          note="Süresi geçmiş, karar bekliyor"
          isPending={query.isPending}
          to={`${ROUTES.cvReviews}?status=Overdue`}
        />
      </div>
      <SortableGrid id="cvReviews.1" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="cvDecisions"
          width={3}
          title="Karar dağılımı"
          hint="Dönemde önerilen CV'ler"
          query={query}
          isEmpty={decisions.every((item) => item.count === 0)}
          emptyText="Bu dönemde CV önerilmedi."
          table={{
            columns: ['Karar', 'CV'],
            rows: decisions.map((item) => [item.label, item.count]),
          }}
          to={ROUTES.cvReviews}
        >
          <DoughnutChart items={decisions} colors={DECISION_COLORS} centerLabel="CV" />
        </ChartCard>

        <ChartCard
          cardId="cvWaiting"
          width={3}
          title="Kimde ne kadar bekliyor"
          hint="Karar bekleyen CV'ler, değerlendirici bazında"
          query={query}
          isEmpty={reviewers.length === 0}
          emptyText="Karar bekleyen CV yok."
          table={{
            columns: ['Değerlendirici', 'Bekleyen CV'],
            rows: reviewers.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={reviewers} unit="CV" color="--color-warning" />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
