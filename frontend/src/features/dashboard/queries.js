import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/shared/components/toast';
import {
  getDashboardLayout,
  getDashboardSection,
  resetDashboardLayout,
  saveDashboardLayout,
} from './api';

export const dashboardKeys = {
  all: ['dashboard'],
  section: (section, filters) => ['dashboard', section, filters],
};

/**
 * Bir dashboard temasının verisi. Filtre değişince önceki veri ekranda kalır (kartlar yanıp sönmez);
 * veri bir dakika taze sayılır.
 * @param {import('./api').DashboardSection} section @param {import('./api').DashboardFilters} filters
 */
export function useDashboardSection(section, filters) {
  return useQuery({
    queryKey: dashboardKeys.section(section, filters),
    queryFn: () => getDashboardSection(section, filters),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  });
}

const layoutKey = ['preferences', 'dashboard.layout'];

/**
 * Kullanıcının kart düzeni. Değişiklik ekranda hemen görünür, arka planda kaydedilir;
 * kayıt başarısız olursa sunucudaki düzen geri yüklenir.
 */
export function useDashboardLayout() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const query = useQuery({ queryKey: layoutKey, queryFn: getDashboardLayout, staleTime: Infinity });

  const onError = () => {
    queryClient.invalidateQueries({ queryKey: layoutKey });
    toast.error('Kart düzeni kaydedilemedi.');
  };

  const save = useMutation({
    mutationFn: saveDashboardLayout,
    onMutate: (layout) => queryClient.setQueryData(layoutKey, layout),
    onError,
  });

  const reset = useMutation({
    mutationFn: resetDashboardLayout,
    onMutate: () => queryClient.setQueryData(layoutKey, null),
    onError,
  });

  return { layout: query.data ?? null, save: save.mutate, reset: reset.mutate };
}
