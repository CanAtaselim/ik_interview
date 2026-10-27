import { ROUTES } from '@/shared/constants/routes';
import BarChart from '../charts/BarChart';
import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import DoughnutChart from '../charts/DoughnutChart';
import Heatmap, { DAY_NAMES } from '../charts/Heatmap';
import KpiCard from '../charts/KpiCard';
import { useDashboardSection } from '../queries';

// Planlandı, tamamlandı, iptal, gelmedi (sunucudaki sıra).
const STATUS_COLORS = ['--chart-1', '--color-success', '--color-border-strong', '--color-danger'];

/** Mülakatlar: durum, tip, mülakatçı yükü, yoğunluk. @param {{ filters: import('../api').DashboardFilters }} props */
export default function InterviewCards({ filters }) {
  const query = useDashboardSection('interviews', filters);
  const { data } = query;

  const byStatus = data?.byStatus ?? [];
  const byType = data?.byType ?? [];
  const load = data?.interviewerLoad ?? [];
  const heatmap = data?.heatmap ?? [];

  return (
    <>
      <h2 className="dashboard-heading">Mülakatlar</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="Dönemdeki mülakat"
          value={data?.inPeriod}
          isPending={query.isPending}
          to={ROUTES.interviews}
        />
        <KpiCard
          label="Önümüzdeki 7 gün"
          value={data?.upcoming}
          note="Planlı mülakat"
          isPending={query.isPending}
          to={ROUTES.interviews}
        />
        <KpiCard
          label="Sonucu girilmemiş"
          value={data?.awaitingOutcome}
          tone={data?.awaitingOutcome ? 'danger' : 'default'}
          note="Saati geçmiş mülakat"
          isPending={query.isPending}
          to={ROUTES.interviews}
        />
      </div>
      <SortableGrid id="interviews.1" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="interviewStatus"
          width={2}
          title="Mülakat durumu"
          hint="Dönemdeki mülakatlar"
          query={query}
          isEmpty={byStatus.every((item) => item.count === 0)}
          emptyText="Bu dönemde mülakat yok."
          table={{
            columns: ['Durum', 'Mülakat'],
            rows: byStatus.map((item) => [item.label, item.count]),
          }}
        >
          <DoughnutChart items={byStatus} colors={STATUS_COLORS} centerLabel="Mülakat" />
        </ChartCard>

        <ChartCard
          cardId="interviewType"
          width={2}
          title="Mülakat tipi"
          hint="İptaller hariç"
          query={query}
          isEmpty={byType.length === 0}
          emptyText="Bu dönemde mülakat yok."
          table={{
            columns: ['Tip', 'Mülakat'],
            rows: byType.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={byType} unit="mülakat" />
        </ChartCard>

        <ChartCard
          cardId="interviewerLoad"
          width={2}
          title="Mülakatçı yükü"
          hint="Mülakatçı başına mülakat sayısı (iptaller hariç)"
          query={query}
          isEmpty={load.length === 0}
          emptyText="Bu dönemde mülakat yok."
          table={{
            columns: ['Mülakatçı', 'Mülakat'],
            rows: load.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={load} unit="mülakat" color="--chart-7" />
        </ChartCard>

        <ChartCard
          cardId="interviewHeatmap"
          width={6}
          title="Yoğunluk haritası"
          hint="Mülakatların gün ve saate göre dağılımı"
          query={query}
          isEmpty={heatmap.length === 0}
          emptyText="Bu dönemde mülakat yok."
          table={{
            columns: ['Gün', 'Saat', 'Mülakat'],
            rows: heatmap.map((cell) => [
              DAY_NAMES[cell.day],
              `${String(cell.hour).padStart(2, '0')}:00`,
              cell.count,
            ]),
          }}
        >
          <Heatmap cells={heatmap} unit="mülakat" />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
