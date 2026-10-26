import { Skeleton } from 'primereact/skeleton';
import './TableSkeleton.css';

/**
 * Liste ilk kez yüklenirken tablo yerine gösterilen iskelet.
 * @param {{ rows?: number }} props
 */
export default function TableSkeleton({ rows = 5 }) {
  return (
    <div className="table-skeleton" aria-busy="true" aria-label="Liste yükleniyor">
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} height="var(--control-height)" />
      ))}
    </div>
  );
}
