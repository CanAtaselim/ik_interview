import { ROUTES } from '@/shared/constants/routes';
import BarChart from '../charts/BarChart';
import SortableGrid from '../components/SortableGrid';
import ChartCard from '../charts/ChartCard';
import { formatDays } from '../charts/chartTheme';
import DoughnutChart from '../charts/DoughnutChart';
import KpiCard from '../charts/KpiCard';
import ProgressList from '../charts/ProgressList';
import { useDashboardSection } from '../queries';

// Taslak, onay bekliyor, onaylandı, reddedildi, iptal (sunucudaki sıra).
const STATUS_COLORS = [
  '--color-border-strong',
  '--color-warning',
  '--color-success',
  '--color-danger',
  '--color-text-muted',
];

/** Kadro talepleri ve pozisyonlar. @param {{ filters: import('../api').DashboardFilters }} props */
export default function RequestCards({ filters }) {
  const query = useDashboardSection('requests', filters);
  const { data } = query;

  const byStatus = data?.byStatus ?? [];
  const byReason = data?.byReason ?? [];
  const byDepartment = data?.openByDepartment ?? [];
  const openPositions = data?.openPositions ?? [];

  return (
    <>
      <h2 className="dashboard-heading">Kadro talepleri ve pozisyonlar</h2>
      <div className="dashboard-kpis">
        <KpiCard
          label="Onay bekleyen talep"
          value={data?.pendingApproval}
          note="Şu an"
          isPending={query.isPending}
          to={`${ROUTES.hiringRequests}?awaiting=1`}
        />
        <KpiCard
          label="Ortalama onay süresi"
          value={data?.averageApprovalDays}
          format={formatDays}
          note="Gönderimden karara"
          isPending={query.isPending}
        />
      </div>
      <SortableGrid id="requests.1" className="dashboard-grid dashboard-grid--after-kpis">
        <ChartCard
          cardId="requestStatus"
          width={2}
          title="Talep durumu"
          hint="Dönemde açılan talepler"
          query={query}
          isEmpty={byStatus.every((item) => item.count === 0)}
          emptyText="Bu dönemde talep açılmadı."
          table={{
            columns: ['Durum', 'Talep'],
            rows: byStatus.map((item) => [item.label, item.count]),
          }}
          to={ROUTES.hiringRequests}
        >
          <DoughnutChart items={byStatus} colors={STATUS_COLORS} centerLabel="Talep" />
        </ChartCard>

        <ChartCard
          cardId="requestReason"
          width={2}
          title="Talep nedeni"
          hint="Yeni kadro veya ikame"
          query={query}
          isEmpty={byReason.every((item) => item.count === 0)}
          emptyText="Bu dönemde talep açılmadı."
          table={{
            columns: ['Neden', 'Talep'],
            rows: byReason.map((item) => [item.label, item.count]),
          }}
        >
          <DoughnutChart items={byReason} centerLabel="Talep" />
        </ChartCard>

        <ChartCard
          cardId="openByDepartment"
          width={2}
          title="Birim bazında açık kadro"
          hint="Açık pozisyonlarda henüz dolmamış kişi sayısı"
          query={query}
          isEmpty={byDepartment.length === 0}
          emptyText="Dolmamış açık kadro yok."
          table={{
            columns: ['Birim', 'Açık kadro'],
            rows: byDepartment.map((item) => [item.label, item.count]),
          }}
        >
          <BarChart items={byDepartment} unit="kişi" />
        </ChartCard>

        <ChartCard
          cardId="positionFill"
          width={6}
          title="Açık pozisyonlar ve doluluk"
          hint="En çok açık kadrosu olanlar"
          query={query}
          height={120}
          isEmpty={openPositions.length === 0}
          emptyText="Açık pozisyon yok."
          to={ROUTES.positions}
          toLabel="Tüm pozisyonlar"
        >
          <ProgressList
            toItem={ROUTES.pipeline}
            items={openPositions.map((position) => ({
              id: position.id,
              title: position.title,
              subtitle: position.department,
              value: position.filled,
              target: position.headcount,
            }))}
          />
        </ChartCard>
      </SortableGrid>
    </>
  );
}
