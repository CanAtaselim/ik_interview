import { Skeleton } from 'primereact/skeleton';
import './layout.css';

const ROW_HEIGHT = 'var(--control-height)';

/** Uygulama açılırken oturum geri yüklenene kadar gösterilen iskelet. */
export default function AppSkeleton() {
  return (
    <div className="app-shell" aria-busy="true" aria-label="Uygulama yükleniyor">
      <aside className="app-sidebar">
        <div className="app-sidebar__brand">
          <Skeleton height={ROW_HEIGHT} />
        </div>
        <div className="app-sidebar__list">
          <Skeleton height={ROW_HEIGHT} />
          <Skeleton height={ROW_HEIGHT} />
          <Skeleton height={ROW_HEIGHT} />
        </div>
      </aside>
      <div className="app-main">
        <div className="app-topbar" />
        <div className="app-content app-content--skeleton">
          <Skeleton width="40%" height={ROW_HEIGHT} />
          <Skeleton height="calc(var(--control-height) * 5)" />
        </div>
      </div>
    </div>
  );
}
