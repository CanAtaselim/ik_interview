import { ROUTES } from '@/shared/constants/routes';
import BarChart from '../charts/BarChart';
import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import { formatNumber } from '../charts/chartTheme';
import KpiCard from '../charts/KpiCard';
import LineChart, { formatBucket } from '../charts/LineChart';
import { useDashboardSection } from '../queries';

const rate = (hired, total) => (total === 0 ? 0 : Math.round((hired / total) * 1000) / 10);

/** Aday havuzu: büyüklük, eklenenler, kaynak, kaynak verimi, etiketler. @param {{ filters: import('../api').DashboardFilters }} props */
export default function CandidateCards({ filters }) {
  const query = useDashboardSection('candidates', filters);
  const { data } = query;

  const timeline = data?.timeline ?? [];
  const bySource = data?.bySource ?? [];
  const conversion = (data?.sourceConversion ?? []).filter((source) => source.candidates > 0);
  const tags = data?.topTags ?? [];

  return (
    <>
      <h2 className="dashboard-heading">Aday havuzu</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="Toplam aday"
          value={data?.total}
          isPending={query.isPending}
          to={ROUTES.candidates}
        />
        <KpiCard
          label="Havuzdaki aday"
          value={data?.inTalentPool}
          note="Beklemede tutulan"
          isPending={query.isPending}
        />
        <KpiCard
          label="Dönemde eklenen"
          value={data?.added.value}
          previous={data?.added.previous}
          isPending={query.isPending}
        />
      </div>
      <SortableGrid id="candidates.1" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="candidateTimeline"
          width={3}
          title="Zaman içinde eklenen aday"
          hint={
            data
              ? `${{ day: 'Günlük', week: 'Haftalık', month: 'Aylık' }[data.granularity]} aday girişi`
              : undefined
          }
          query={query}
          isEmpty={timeline.every((point) => point.count === 0)}
          emptyText="Bu dönemde aday eklenmedi."
          table={{
            columns: ['Dönem başlangıcı', 'Aday'],
            rows: timeline.map((point) => [
              formatBucket(point.date, data.granularity),
              point.count,
            ]),
          }}
        >
          <LineChart
            points={timeline}
            granularity={data?.granularity ?? 'day'}
            seriesLabel="Aday"
          />
        </ChartCard>

        <ChartCard
          cardId="candidateSource"
          width={3}
          title="Aday kaynağı"
          hint="Dönemde eklenen adayların geldiği kanal"
          query={query}
          isEmpty={bySource.length === 0}
          emptyText="Bu dönemde aday eklenmedi."
          table={{
            columns: ['Kaynak', 'Aday'],
            rows: bySource.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={bySource} unit="aday" />
        </ChartCard>

        <ChartCard
          cardId="sourceYield"
          width={3}
          title="Kaynak verimi"
          hint="Tüm zamanlar: kaynaktaki adayın işe alınma oranı (%)"
          query={query}
          isEmpty={conversion.length === 0}
          emptyText="Henüz aday yok."
          table={{
            columns: ['Kaynak', 'Aday', 'İşe alınan', 'Oran'],
            rows: conversion.map((item) => [
              item.source,
              item.candidates,
              item.hired,
              `%${formatNumber(rate(item.hired, item.candidates))}`,
            ]),
          }}
        >
          <BarChart
            items={conversion.map((item) => ({
              label: item.source,
              count: rate(item.hired, item.candidates),
            }))}
            unit="%"
            decimals
            color="--chart-3"
          />
        </ChartCard>

        <ChartCard
          cardId="topTags"
          width={3}
          title="En çok kullanılan etiketler"
          hint="CV'den okunan yetenekler dahil"
          query={query}
          isEmpty={tags.length === 0}
          emptyText="Henüz etiket yok."
          table={{
            columns: ['Etiket', 'Aday'],
            rows: tags.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={tags} unit="aday" color="--chart-7" />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
