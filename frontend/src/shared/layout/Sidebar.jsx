import { NavLink } from 'react-router';
import { useMyTasks } from '@/features/dashboard/useMyTasks';
import { useAuth } from '@/shared/auth/useAuth';
import { ROUTES } from '@/shared/constants/routes';

/**
 * @typedef {{ path: string, label: string, icon: string, roles?: readonly string[] | null }} NavigationItem
 */

/**
 * Sol menü. Kullanıcının yetkisi olmayan öğeler hiç gösterilmez.
 * @param {{ navigation: NavigationItem[], collapsed: boolean, onNavigate: () => void }} props
 */
export default function Sidebar({ navigation, collapsed, onNavigate }) {
  const { hasAnyRole } = useAuth();
  const visibleItems = navigation.filter((item) => hasAnyRole(item.roles));
  // Bekleyen iş sayısı "Bekleyen işlerim" menüsünün yanında görünür.
  const { count: taskCount } = useMyTasks();

  return (
    <aside className="app-sidebar">
      <div className="app-sidebar__brand">
        <span className="app-sidebar__logo" aria-hidden="true">
          <i className="pi pi-users" />
        </span>
        <span className="app-sidebar__name">İK İşe Alım</span>
      </div>

      <nav aria-label="Ana menü">
        <ul className="app-sidebar__list">
          {visibleItems.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.path === ROUTES.home}
                className="app-sidebar__link"
                title={collapsed ? item.label : undefined}
                onClick={onNavigate}
              >
                <i className={item.icon} aria-hidden="true" />
                <span className="app-sidebar__label">{item.label}</span>
                {item.path === ROUTES.home && taskCount > 0 && (
                  <span
                    className="app-sidebar__badge"
                    role="status"
                    aria-label={`${taskCount} bekleyen iş`}
                  >
                    {taskCount > 99 ? '99+' : taskCount}
                  </span>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}
