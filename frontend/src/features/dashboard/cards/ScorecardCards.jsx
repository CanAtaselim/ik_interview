import { ROUTES } from '@/shared/constants/routes';
import BarChart from '../charts/BarChart';
import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import { formatNumber } from '../charts/chartTheme';
import DoughnutChart from '../charts/DoughnutChart';
import KpiCard from '../charts/KpiCard';
import { useDashboardSection } from '../queries';

// Kesinlikle işe al, işe al, kararsız, işe alma, kesinlikle alma (sunucudaki sıra).
const RECOMMENDATION_COLORS = [
  '--color-success',
  '--chart-3',
  '--color-warning',
  '--chart-2',
  '--color-danger',
];

const asItems = (scores) => scores.map((score) => ({ label: score.label, count: score.average }));
const asRows = (scores) =>
  scores.map((score) => [score.label, formatNumber(score.average), score.count]);

/** Değerlendirme ve puanlama. @param {{ filters: import('../api').DashboardFilters }} props */
export default function ScorecardCards({ filters }) {
  const query = useDashboardSection('scorecards', filters);
  const { data } = query;

  const recommendations = data?.recommendations ?? [];
  const competencies = data?.competencyAverages ?? [];
  const interviewers = data?.interviewerScoring ?? [];
  const pending = data?.pendingByInterviewer ?? [];

  return (
    <>
      <h2 className="dashboard-heading">Değerlendirme ve puanlama</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="Gönderilmemiş değerlendirme"
          value={data?.pending}
          tone={data?.pending ? 'danger' : 'default'}
          note="Mülakat yapılmış, puan bekleniyor"
          isPending={query.isPending}
          to={ROUTES.interviews}
        />
        <KpiCard label="Dönemde gönderilen" value={data?.submitted} isPending={query.isPending} />
      </div>
      <SortableGrid id="scorecards.1" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="recommendations"
          width={2}
          title="Mülakatçı önerileri"
          hint="Dönemde gönderilen değerlendirmelerin genel önerisi"
          query={query}
          isEmpty={recommendations.every((item) => item.count === 0)}
          emptyText="Bu dönemde gönderilen değerlendirme yok."
          table={{
            columns: ['Öneri', 'Değerlendirme'],
            rows: recommendations.map((item) => [item.label, item.count]),
          }}
        >
          <DoughnutChart
            items={recommendations}
            colors={RECOMMENDATION_COLORS}
            centerLabel="Değerlendirme"
          />
        </ChartCard>

        <ChartCard
          cardId="competencies"
          width={2}
          title="Yetkinlik ortalamaları"
          hint="Verilen puanların ağırlıklı ortalaması (1-5)"
          query={query}
          isEmpty={competencies.length === 0}
          emptyText="Yetkinlik puanı verilmemiş."
          table={{ columns: ['Yetkinlik', 'Ortalama', 'Puanlı soru'], rows: asRows(competencies) }}
        >
          <BarChart items={asItems(competencies)} max={5} decimals unit="/ 5" color="--chart-3" />
        </ChartCard>

        <ChartCard
          cardId="interviewerTrend"
          width={2}
          title="Mülakatçı puan eğilimi"
          hint="Mülakatçının verdiği ortalama puan (1-5); kimin sert ya da yumuşak puanladığını gösterir"
          query={query}
          isEmpty={interviewers.length === 0}
          emptyText="Bu dönemde gönderilen değerlendirme yok."
          table={{
            columns: ['Mülakatçı', 'Ortalama puan', 'Değerlendirme'],
            rows: asRows(interviewers),
          }}
        >
          <BarChart items={asItems(interviewers)} max={5} decimals unit="/ 5" color="--chart-7" />
        </ChartCard>

        <ChartCard
          cardId="scorecardWaiting"
          width={6}
          title="Kimde puan bekliyor"
          hint="Gönderilmemiş değerlendirmeler, mülakatçı bazında"
          query={query}
          isEmpty={pending.length === 0}
          emptyText="Bekleyen değerlendirme yok."
          table={{
            columns: ['Mülakatçı', 'Bekleyen'],
            rows: pending.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={pending} unit="değerlendirme" color="--color-warning" />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
