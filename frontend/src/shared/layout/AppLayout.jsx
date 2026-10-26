import { useState } from 'react';
import { Outlet } from 'react-router';
import { useNotificationStream } from '@/shared/notifications/useNotificationStream';
import Sidebar from './Sidebar';
import Topbar from './Topbar';
import { useUiStore } from './uiStore';
import './layout.css';

// layout.css'teki mobil kırılım noktasıyla aynı olmalı.
const MOBILE_QUERY = '(max-width: 768px)';

/**
 * Oturum açmış kullanıcının çerçevesi: sol menü + üst çubuk + sayfa içeriği.
 * @param {{ navigation: import('./Sidebar').NavigationItem[] }} props
 */
export default function AppLayout({ navigation }) {
  const collapsed = useUiStore((state) => state.sidebarCollapsed);
  const toggleCollapsed = useUiStore((state) => state.toggleSidebar);
  const [mobileOpen, setMobileOpen] = useState(false);
  useNotificationStream();

  // Masaüstünde menü daralır; mobilde içeriğin üzerine açılan çekmece olur.
  const toggleMenu = () => {
    if (window.matchMedia(MOBILE_QUERY).matches) setMobileOpen((open) => !open);
    else toggleCollapsed();
  };

  return (
    <div className="app-shell" data-collapsed={collapsed} data-mobile-open={mobileOpen}>
      <Sidebar
        navigation={navigation}
        collapsed={collapsed}
        onNavigate={() => setMobileOpen(false)}
      />
      <button
        type="button"
        className="app-shell__backdrop"
        aria-label="Menüyü kapat"
        onClick={() => setMobileOpen(false)}
      />
      <div className="app-main">
        <Topbar onToggleMenu={toggleMenu} />
        <main className="app-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
