import { useCallback, useState } from 'react';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';

/**
 * İndirme işlemini yükleniyor durumu ve Türkçe hata bildirimiyle sarar. Aynı anda tek indirme çalışır.
 * @returns {{ pending: boolean, run: (task: () => Promise<void>) => Promise<void> }}
 */
export function useDownload() {
  const toast = useToast();
  const [pending, setPending] = useState(false);

  const run = useCallback(
    async (task) => {
      setPending(true);
      try {
        await task();
      } catch (error) {
        toast.error(getErrorMessage(error));
      } finally {
        setPending(false);
      }
    },
    [toast],
  );

  return { pending, run };
}
