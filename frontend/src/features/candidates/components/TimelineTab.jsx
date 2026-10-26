import { useQuery } from '@tanstack/react-query';
import EmptyState from '@/shared/components/EmptyState';
import TableSkeleton from '@/shared/components/TableSkeleton';
import Timeline from '@/shared/components/Timeline';
import { getTimeline } from '../api';
import { candidateKeys } from '../queries';

// Olaylar arka planda yazıldığı için (birkaç saniye gecikmeyle) sekme açıkken düzenli tazelenir.
const REFRESH_MS = 10_000;

/** @param {{ candidateId: string }} props */
export default function TimelineTab({ candidateId }) {
  const { data, isLoading } = useQuery({
    queryKey: candidateKeys.timeline(candidateId),
    queryFn: () => getTimeline(candidateId),
    staleTime: 0,
    refetchInterval: REFRESH_MS,
  });

  if (isLoading) return <TableSkeleton rows={4} />;

  return data.items.length > 0 ? (
    <Timeline entries={data.items} />
  ) : (
    <EmptyState
      icon="pi pi-history"
      title="Henüz kayıtlı olay yok"
      description="Aday üzerindeki her işlem birkaç saniye içinde burada görünür."
    />
  );
}
