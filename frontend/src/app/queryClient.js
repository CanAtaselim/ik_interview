import { QueryClient } from '@tanstack/react-query';

const MAX_RETRIES = 2;

// 4xx yanıtları (yetki, bulunamadı, validasyon) tekrar denemekle düzelmez.
function shouldRetry(failureCount, error) {
  const status = error?.response?.status;
  const isClientError = status >= 400 && status < 500;
  return !isClientError && failureCount < MAX_RETRIES;
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: shouldRetry,
    },
  },
});
