import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import { formatDays, formatNumber } from '../charts/chartTheme';
import BarChart from '../charts/BarChart';
import FunnelChart from '../charts/FunnelChart';
import KpiCard from '../charts/KpiCard';
import { useDashboardSection } from '../queries';

const percent = (part, whole) => (whole === 0 ? 0 : Math.round((part / whole) * 100));

/** İşe alım süreci: huni, aşamada bekleme, ret nedenleri, işe alım süreleri. @param {{ filters: import('../api').DashboardFilters }} props */
export default function PipelineCards({ filters }) {
  const query = useDashboardSection('pipeline', filters);
  const { data } = query;

  const funnel = data?.funnel ?? [];
  const durations = data?.stageDurations ?? [];
  const rejection = data?.rejectionByStage ?? [];
  const reasons = data?.rejectionReasons ?? [];
  const byDepartment = data?.timeToHireByDepartment ?? [];

  return (
    <>
      <h2 className="dashboard-heading">İşe alım süreci</h2>
      <SortableGrid id="pipeline.1" className="dashboard-grid">
        <ChartCard
          cardId="funnel"
          width={3}
          title="İşe alım hunisi"
          hint="Dönemde başlayan süreçlerin her aşamaya ulaşma sayısı"
          query={query}
          isEmpty={funnel.every((stage) => stage.count === 0)}
          emptyText="Bu dönemde başlayan süreç yok."
          table={{
            columns: ['Aşama', 'Süreç sayısı'],
            rows: funnel.map((stage) => [stage.stage, stage.count]),
          }}
        >
          <FunnelChart stages={funnel} />
        </ChartCard>

        <ChartCard
          cardId="rejectionReasons"
          width={3}
          title="Ret nedenleri"
          hint="Dönemde sonlanan süreçler"
          query={query}
          isEmpty={reasons.length === 0}
          emptyText="Bu dönemde sonlanan süreç yok."
          table={{
            columns: ['Neden', 'Süreç sayısı'],
            rows: reasons.map((r) => [r.label, r.count]),
          }}
        >
          <BarChart items={reasons} unit="süreç" />
        </ChartCard>

        <ChartCard
          cardId="stageDurations"
          width={3}
          title="Aşamada kalma süresi"
          hint="Dönemde aşamadan çıkanların ortalama süresi (gün). Uzun olan aşama darboğazdır."
          query={query}
          isEmpty={durations.every((d) => d.averageDays === null && d.waiting === 0)}
          emptyText="Henüz ölçülebilir aşama geçişi yok."
          table={{
            columns: ['Aşama', 'Ortalama süre', 'Şu an bekleyen', 'Bekleyenin ortalama süresi'],
            rows: durations.map((d) => [
              d.stage,
              formatDays(d.averageDays),
              d.waiting,
              formatDays(d.averageWaitingDays),
            ]),
          }}
        >
          <BarChart
            items={durations
              .filter((d) => d.averageDays !== null)
              .map((d) => ({ label: d.stage, count: d.averageDays }))}
            unit="gün"
            decimals
          />
        </ChartCard>

        <ChartCard
          cardId="rejectionRate"
          width={3}
          title="Aşamada ret oranı"
          hint="Aşamaya ulaşanların kaçı orada sonlandı (%)"
          query={query}
          isEmpty={rejection.every((r) => r.closed === 0)}
          emptyText="Bu dönemde sonlanan süreç yok."
          table={{
            columns: ['Aşama', 'Ulaşan', 'Sonlanan', 'Oran'],
            rows: rejection.map((r) => [
              r.stage,
              r.reached,
              r.closed,
              `%${percent(r.closed, r.reached)}`,
            ]),
          }}
        >
          <BarChart
            items={rejection
              .filter((r) => r.reached > 0)
              .map((r) => ({ label: r.stage, count: percent(r.closed, r.reached) }))}
            unit="%"
            max={100}
            color="--chart-2"
          />
        </ChartCard>
      </SortableGrid>

      <h2 className="dashboard-heading">Süreler</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="İşe alım süresi"
          value={data?.timeToHire.averageDays}
          previous={data?.timeToHire.previousAverageDays}
          lowerIsBetter
          format={formatDays}
          note={
            data?.timeToHire.count
              ? `Ortanca ${formatDays(data.timeToHire.medianDays)} · ${data.timeToHire.count} işe alım`
              : 'Bu dönemde işe alım yok'
          }
          isPending={query.isPending}
        />
        <KpiCard
          label="Kadro doldurma süresi"
          value={data?.timeToFill.averageDays}
          previous={data?.timeToFill.previousAverageDays}
          lowerIsBetter
          format={formatDays}
          note={
            data?.timeToFill.count
              ? `${formatNumber(data.timeToFill.count)} pozisyon doldu`
              : 'Bu dönemde dolan pozisyon yok'
          }
          isPending={query.isPending}
        />
      </div>
      <SortableGrid id="pipeline.2" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="hireByDepartment"
          width={6}
          title="Birim bazında işe alım süresi"
          hint="Başvurudan işe alınmaya ortalama gün"
          query={query}
          isEmpty={byDepartment.length === 0}
          emptyText="Bu dönemde işe alım yok."
          table={{
            columns: ['Birim', 'Ortalama süre', 'İşe alım'],
            rows: byDepartment.map((d) => [d.label, formatDays(d.averageDays), d.count]),
          }}
        >
          <BarChart
            items={byDepartment.map((d) => ({ label: d.label, count: d.averageDays }))}
            unit="gün"
            decimals
          />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
