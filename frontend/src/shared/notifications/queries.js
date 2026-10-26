import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '@/shared/api/notifications';

export const notificationKeys = {
  all: ['notifications'],
  list: ['notifications', 'list'],
  preferences: ['notifications', 'preferences'],
};

const LIST_SIZE = 20;

/** Zil menüsündeki son bildirimler ve okunmamış sayısı. */
export function useNotifications() {
  return useQuery({
    queryKey: notificationKeys.list,
    queryFn: () => api.getNotifications({ pageSize: LIST_SIZE }),
    // Anlık bağlantı koparsa sayaç yine de güncellensin.
    refetchInterval: 60_000,
  });
}

function useNotificationMutation(mutationFn) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}

export const useMarkNotificationRead = () => useNotificationMutation(api.markNotificationRead);

export const useMarkAllNotificationsRead = () =>
  useNotificationMutation(api.markAllNotificationsRead);

export function useNotificationPreferences() {
  return useQuery({
    queryKey: notificationKeys.preferences,
    queryFn: api.getNotificationPreferences,
  });
}

export const useUpdateNotificationPreferences = () =>
  useNotificationMutation(api.updateNotificationPreferences);
