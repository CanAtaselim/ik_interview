import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from './api';

export const candidateKeys = {
  all: ['candidates'],
  list: (filters) => ['candidates', 'list', filters],
  detail: (id) => ['candidates', 'detail', id],
  timeline: (id) => ['candidates', 'timeline', id],
};

/** @param {Parameters<typeof api.getCandidates>[0]} filters */
export function useCandidates(filters) {
  return useQuery({
    queryKey: candidateKeys.list(filters),
    queryFn: () => api.getCandidates(filters),
    placeholderData: keepPreviousData,
  });
}

/** @param {string} id */
export function useCandidate(id) {
  return useQuery({ queryKey: candidateKeys.detail(id), queryFn: () => api.getCandidate(id) });
}

/** @param {string} id */
export function useCandidateTimeline(id) {
  return useQuery({ queryKey: candidateKeys.timeline(id), queryFn: () => api.getTimeline(id) });
}

// Adayla ilgili her değişiklik listeyi, kartı ve zaman çizelgesini etkileyebilir; birlikte tazelenir.
function useCandidateMutation(mutationFn) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: candidateKeys.all }),
  });
}

export const useCreateCandidate = () => useCandidateMutation(api.createCandidate);

export const useUpdateCandidate = () =>
  useCandidateMutation(({ id, input }) => api.updateCandidate(id, input));

export const useSetTalentPool = () =>
  useCandidateMutation(({ id, inTalentPool }) => api.setTalentPool(id, inTalentPool));

export const useUploadDocument = () =>
  useCandidateMutation(({ id, file, typeId }) => api.uploadDocument(id, file, typeId));

export const useDeleteDocument = () =>
  useCandidateMutation(({ id, documentId }) => api.deleteDocument(id, documentId));

export const useStartScreening = () => useCandidateMutation(api.startScreening);

export const useDeleteScreening = () =>
  useCandidateMutation(({ id, screeningId }) => api.deleteScreening(id, screeningId));

/** Otomatik kayıt için: yazarken listeleri tazelemez, yalnızca kaydeder. */
export const useSaveScreening = () =>
  useMutation({
    mutationFn: ({ id, screeningId, input }) => api.updateScreening(id, screeningId, input),
  });
