import { Button } from 'primereact/button';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import { downloadListReport } from '../api';
import { useDownload } from '../useDownload';

/**
 * Bir ekranın listesini, ekrandaki filtrelerle Excel raporu olarak indirir. Raporlar İK'ya açıktır;
 * diğer rollere düğme gösterilmez.
 * @param {{ kind: string, filters?: import('../api').ListReportFilters, label?: string,
 *   disabledReason?: string }} props
 *   `disabledReason` doluysa düğme pasif olur ve nedeni ipucu olarak gösterir (rapora uygulanamayan filtre).
 */
export default function ReportButton({ kind, filters = {}, label = 'Excel raporu', disabledReason }) {
  const { hasAnyRole } = useAuth();
  const { pending, run } = useDownload();

  if (!hasAnyRole(HR_ROLES)) return null;

  return (
    <Button
      type="button"
      label={label}
      icon="pi pi-file-excel"
      outlined
      severity="secondary"
      loading={pending}
      disabled={Boolean(disabledReason)}
      tooltip={disabledReason ?? 'Ekrandaki filtrelerle Excel olarak indirir'}
      tooltipOptions={{ position: 'bottom', showOnDisabled: true }}
      onClick={() => run(() => downloadListReport(kind, filters))}
    />
  );
}
