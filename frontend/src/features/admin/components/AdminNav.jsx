import { NavLink } from 'react-router';
import { Role, SETTINGS_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import { ROUTES } from '@/shared/constants/routes';
import './AdminNav.css';

/** Yönetim bölümündeki sayfalar. Kullanıcının rolü yetmeyenler gösterilmez. */
export const ADMIN_PAGES = Object.freeze([
  { path: ROUTES.dictionaries, label: 'Sözlükler', roles: [Role.SystemAdmin, Role.HrManager] },
  { path: ROUTES.approvalFlow, label: 'Onay akışı', roles: SETTINGS_ROLES },
  { path: ROUTES.pipelineStages, label: 'Süreç aşamaları', roles: SETTINGS_ROLES },
  { path: ROUTES.emailTemplates, label: 'E-posta şablonları', roles: SETTINGS_ROLES },
  { path: ROUTES.holidays, label: 'Resmi tatiller', roles: SETTINGS_ROLES },
  { path: ROUTES.roleMappings, label: 'Rol eşlemesi', roles: [Role.SystemAdmin] },
]);

/** Yönetim sayfalarının üstündeki sekme şeridi. */
export default function AdminNav() {
  const { hasAnyRole } = useAuth();

  return (
    <nav className="admin-nav" aria-label="Yönetim">
      {ADMIN_PAGES.filter((page) => hasAnyRole(page.roles)).map((page) => (
        <NavLink key={page.path} to={page.path} className="admin-nav__link">
          {page.label}
        </NavLink>
      ))}
    </nav>
  );
}
