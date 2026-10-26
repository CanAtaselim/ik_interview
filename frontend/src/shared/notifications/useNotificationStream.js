import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';
import { refreshSession } from '@/shared/api/http';
import { useAuthStore } from '@/shared/auth/authStore';
import { useToast } from '@/shared/components/toast';
import { notificationKeys } from './queries';

const HUB_PATH = '/hubs/notifications';
const RETRY_MS = 10_000;

// Bildirim türü → tazelenecek ekran verisi (ör. CV önerilince "Bekleyen işlerim" listesi).
const AFFECTED_QUERIES = {
  HiringRequestAwaitingApproval: [['hiring-requests']],
  HiringRequestDecided: [['hiring-requests'], ['positions']],
  CvReviewRequested: [['cv-reviews']],
  CvReviewReminder: [['cv-reviews']],
  CvReviewEscalated: [['cv-reviews']],
  CvReviewDecided: [['cv-reviews'], ['pipeline']],
  InterviewInvitation: [['interviews']],
  InterviewUpdated: [['interviews']],
  InterviewCancelled: [['interviews']],
  InterviewReminder: [['interviews']],
  DebriefReady: [['scorecards'], ['interviews']],
  ScorecardReopened: [['scorecards'], ['interviews']],
  BlacklistApprovalPending: [['blacklist']],
};

/** Hub adresi, API ile aynı sunucuda ("/api/v1" → "/hubs/notifications"). */
function hubUrl() {
  return new URL(
    HUB_PATH,
    new URL(import.meta.env.VITE_API_URL, window.location.origin),
  ).toString();
}

/**
 * Oturum boyunca bildirim kanalına bağlı kalır: yeni bildirim gelince zil sayacı ve ilgili ekranlar
 * tazelenir, kısa bir bildirim gösterilir. Bağlantı koparsa kendiliğinden yeniden bağlanır.
 */
export function useNotificationStream() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();
  const userId = useAuthStore((state) => state.user?.id);
  // Bildirim geldiğinde güncel yönlendirme ve toast kullanılsın; bağlantı yeniden kurulmasın.
  const handlers = useRef({ navigate, toast });
  useEffect(() => {
    handlers.current = { navigate, toast };
  });

  useEffect(() => {
    if (!userId) return undefined;

    let stopped = false;
    let retry = null;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), { accessTokenFactory: () => useAuthStore.getState().accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('notification', (notification) => {
      queryClient.invalidateQueries({ queryKey: notificationKeys.all });
      (AFFECTED_QUERIES[notification.type] ?? []).forEach((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      );
      const { toast: show, navigate: go } = handlers.current;
      if (notification.link)
        show.action(notification.title, { label: 'Aç', onAction: () => go(notification.link) });
      else show.success(notification.title);
    });

    // Bağlantı kurulamazsa (ör. süresi dolmuş token) oturum yenilenip yeniden denenir.
    const start = async () => {
      if (stopped || connection.state !== HubConnectionState.Disconnected) return;
      try {
        await connection.start();
      } catch {
        if (stopped) return;
        try {
          await refreshSession();
        } catch {
          return;
        }
        retry = setTimeout(start, RETRY_MS);
      }
    };
    connection.onclose(() => {
      if (!stopped) retry = setTimeout(start, RETRY_MS);
    });
    start();

    return () => {
      stopped = true;
      clearTimeout(retry);
      connection.stop();
    };
  }, [userId, queryClient]);
}
