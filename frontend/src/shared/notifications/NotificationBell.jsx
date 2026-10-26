import { Button } from 'primereact/button';
import { OverlayPanel } from 'primereact/overlaypanel';
import { useRef } from 'react';
import { Link, useNavigate } from 'react-router';
import { ROUTES } from '@/shared/constants/routes';
import { formatRelative } from '@/shared/utils/format';
import { useMarkAllNotificationsRead, useMarkNotificationRead, useNotifications } from './queries';
import './NotificationBell.css';

/** Üst çubuktaki bildirim zili: okunmamış sayısı ve son bildirimler. */
export default function NotificationBell() {
  const panel = useRef(null);
  const navigate = useNavigate();
  const { data } = useNotifications();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const unread = data?.unreadCount ?? 0;
  const items = data?.items ?? [];

  const open = (notification) => {
    if (!notification.isRead) markRead.mutate(notification.id);
    panel.current.hide();
    if (notification.link) navigate(notification.link);
  };

  return (
    <>
      <Button
        type="button"
        icon="pi pi-bell"
        text
        rounded
        severity="secondary"
        className="notification-bell"
        badge={unread > 0 ? String(unread > 99 ? '99+' : unread) : undefined}
        badgeClassName="p-badge-danger"
        aria-label={unread > 0 ? `Bildirimler, ${unread} okunmamış` : 'Bildirimler'}
        aria-haspopup="dialog"
        onClick={(event) => panel.current.toggle(event)}
      />
      <OverlayPanel ref={panel} className="notification-panel" aria-label="Bildirimler">
        <header className="notification-panel__header">
          <strong>Bildirimler</strong>
          {unread > 0 && (
            <Button
              type="button"
              label="Tümünü okundu işaretle"
              text
              size="small"
              loading={markAll.isPending}
              onClick={() => markAll.mutate()}
            />
          )}
        </header>
        {items.length === 0 ? (
          <p className="notification-panel__empty">Henüz bildiriminiz yok.</p>
        ) : (
          <ul className="notification-list">
            {items.map((notification) => (
              <li key={notification.id}>
                <button
                  type="button"
                  className="notification-item"
                  data-unread={!notification.isRead}
                  onClick={() => open(notification)}
                >
                  <span className="notification-item__dot" aria-hidden="true" />
                  <span className="notification-item__main">
                    <span className="notification-item__title">
                      {notification.title}
                      {!notification.isRead && <span className="visually-hidden"> (okunmadı)</span>}
                    </span>
                    {notification.body && (
                      <span className="notification-item__body">{notification.body}</span>
                    )}
                    <span className="notification-item__time">
                      {formatRelative(notification.createdAt)}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <footer className="notification-panel__footer">
          <Link to={ROUTES.notificationSettings} onClick={() => panel.current.hide()}>
            Bildirim tercihlerim
          </Link>
        </footer>
      </OverlayPanel>
    </>
  );
}
