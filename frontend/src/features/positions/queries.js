import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from './api';

export const positionKeys = {
  all: ['positions'],
  list: (filters) => ['positions', 'list', filters],
  detail: (id) => ['positions', 'detail', id],
};

/** @param {Parameters<typeof api.getPositions>[0]} filters */
export function usePositions(filters) {
  return useQuery({
    queryKey: positionKeys.list(filters),
    queryFn: () => api.getPositions(filters),
    placeholderData: keepPreviousData,
  });
}

/** @param {string} id */
export function usePosition(id) {
  return useQuery({ queryKey: positionKeys.detail(id), queryFn: () => api.getPosition(id) });
}

function usePositionMutation(mutationFn) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: positionKeys.all }),
  });
}

export const useCreatePosition = () => usePositionMutation(api.createPosition);

export const useUpdatePosition = () =>
  usePositionMutation(({ id, input }) => api.updatePosition(id, input));

export const useChangePositionStatus = () =>
  usePositionMutation(({ id, status }) => api.changePositionStatus(id, status));
