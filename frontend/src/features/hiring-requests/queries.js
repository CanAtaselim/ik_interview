import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from './api';

export const hiringRequestKeys = {
  all: ['hiring-requests'],
  list: (filters) => ['hiring-requests', 'list', filters],
  detail: (id) => ['hiring-requests', 'detail', id],
};

/** @param {Parameters<typeof api.getHiringRequests>[0]} filters @param {{ enabled?: boolean }} [options] */
export function useHiringRequests(filters, { enabled = true } = {}) {
  return useQuery({
    queryKey: hiringRequestKeys.list(filters),
    queryFn: () => api.getHiringRequests(filters),
    // Sayfa veya filtre değişirken tablo boşalmasın.
    placeholderData: keepPreviousData,
    enabled,
  });
}

/** @param {string | null} id */
export function useHiringRequest(id) {
  return useQuery({
    queryKey: hiringRequestKeys.detail(id),
    queryFn: () => api.getHiringRequest(id),
    enabled: Boolean(id),
  });
}

// Her değişiklik listeleri, detayı ve "bekleyen işlerim"i etkiler; hepsi birlikte tazelenir.
function useHiringRequestMutation(mutationFn) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: hiringRequestKeys.all }),
  });
}

export const useCreateHiringRequest = () => useHiringRequestMutation(api.createHiringRequest);

export const useUpdateHiringRequest = () =>
  useHiringRequestMutation(({ id, input }) => api.updateHiringRequest(id, input));

export const useSubmitHiringRequest = () => useHiringRequestMutation(api.submitHiringRequest);

export const useApproveHiringRequest = () =>
  useHiringRequestMutation(({ id, note }) => api.approveHiringRequest(id, note));

export const useRejectHiringRequest = () =>
  useHiringRequestMutation(({ id, reason }) => api.rejectHiringRequest(id, reason));

export const useCancelHiringRequest = () => useHiringRequestMutation(api.cancelHiringRequest);
