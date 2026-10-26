import { Avatar } from 'primereact/avatar';
import { Button } from 'primereact/button';
import { Menu } from 'primereact/menu';
import { useRef } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/shared/auth/useAuth';
import { useLogout } from '@/shared/auth/useLogout';
import { formatRoles } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import NotificationBell from '@/shared/notifications/NotificationBell';
import { getInitials } from '@/shared/utils/format';

/**
 * Üst çubuk: menü düğmesi, bildirim zili ve kullanıcı menüsü.
 * @param {{ onToggleMenu: () => void }} props
 */
export default function Topbar({ onToggleMenu }) {
  const { user } = useAuth();
  const logout = useLogout();
  const userMenu = useRef(null);
  const navigate = useNavigate();

  const menuItems = [
    {
      label: 'Bildirim tercihlerim',
      icon: 'pi pi-bell',
      command: () => navigate(ROUTES.notificationSettings),
    },
    { label: 'Çıkış yap', icon: 'pi pi-sign-out', command: () => logout.mutate() },
  ];

  return (
    <header className="app-topbar">
      <Button
        type="button"
        icon="pi pi-bars"
        text
        severity="secondary"
        aria-label="Menüyü aç veya kapat"
        onClick={onToggleMenu}
      />

      <div className="app-topbar__spacer" />

      <NotificationBell />

      <button
        type="button"
        className="app-topbar__user"
        aria-haspopup="menu"
        aria-controls="user-menu"
        onClick={(event) => userMenu.current.toggle(event)}
      >
        <Avatar label={getInitials(user.displayName)} shape="circle" />
        <span className="app-topbar__identity">
          <span className="app-topbar__name">{user.displayName}</span>
          <span className="app-topbar__role">{formatRoles(user.roles)}</span>
        </span>
        <i className="pi pi-angle-down" aria-hidden="true" />
      </button>
      <Menu id="user-menu" ref={userMenu} model={menuItems} popup popupAlignment="right" />
    </header>
  );
}
