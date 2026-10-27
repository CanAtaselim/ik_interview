import { ROUTES } from '@/shared/constants/routes';
import BarChart from '../charts/BarChart';
import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import KpiCard from '../charts/KpiCard';
import { useDashboardSection } from '../queries';

/** Kara liste özeti (birim/pozisyon filtresi uygulanmaz). @param {{ filters: import('../api').DashboardFilters }} props */
export default function BlacklistCards({ filters }) {
  const query = useDashboardSection('blacklist', filters);
  const { data } = query;
  const reasons = data?.byReason ?? [];

  return (
    <>
      <h2 className="dashboard-heading">Kara liste</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="Aktif kayıt"
          value={data?.active}
          isPending={query.isPending}
          to={ROUTES.blacklist}
        />
        <KpiCard
          label="Onay bekleyen"
          value={data?.pendingApproval}
          tone={data?.pendingApproval ? 'danger' : 'default'}
          isPending={query.isPending}
          to={ROUTES.blacklist}
        />
        <KpiCard
          label="Dönemde açılan"
          value={data?.added.value}
          previous={data?.added.previous}
          isPending={query.isPending}
        />
      </div>
      <SortableGrid id="blacklist.1" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="blacklistReasons"
          width={6}
          title="Kara liste nedenleri"
          hint="Aktif kayıtların nedeni"
          query={query}
          isEmpty={reasons.length === 0}
          emptyText="Aktif kara liste kaydı yok."
          table={{
            columns: ['Neden', 'Kayıt'],
            rows: reasons.map((item) => [item.label, item.count]),
          }}
          to={ROUTES.blacklist}
        >
          <BarChart items={reasons} unit="kayıt" color="--color-danger" />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
