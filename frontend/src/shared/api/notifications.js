import { http } from './http';

/**
 * @typedef {{ id: string, type: string, title: string, body: string | null, link: string | null,
 *   createdAt: string, isRead: boolean }} AppNotification
 */

/**
 * Kullanıcının bildirimleri (en yeni başta) ve okunmamış sayısı.
 * @param {{ unreadOnly?: boolean, page?: number, pageSize?: number }} params
 * @returns {Promise<{ items: AppNotification[], totalCount: number, unreadCount: number }>}
 */
export async function getNotifications(params) {
  const { data } = await http.get('/me/notifications', { params });
  return data;
}

/** @param {string} id */
export async function markNotificationRead(id) {
  await http.post(`/me/notifications/${id}/read`);
}

export async function markAllNotificationsRead() {
  await http.post('/me/notifications/read-all');
}

/** @returns {Promise<{ type: string, emailEnabled: boolean }[]>} */
export async function getNotificationPreferences() {
  const { data } = await http.get('/me/notification-preferences');
  return data;
}

/** @param {{ type: string, emailEnabled: boolean }[]} preferences */
export async function updateNotificationPreferences(preferences) {
  await http.put('/me/notification-preferences', { preferences });
}
