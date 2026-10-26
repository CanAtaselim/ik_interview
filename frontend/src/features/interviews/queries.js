import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '@/shared/api/interviews';
import { interviewKeys } from '@/shared/recruitment/queries';

export const availabilityKeys = {
  all: ['availability'],
  mine: (range) => ['availability', 'mine', range],
};

/** @param {{ from: string, to: string } | null} range */
export function useMyAvailability(range) {
  return useQuery({
    queryKey: availabilityKeys.mine(range),
    queryFn: () => api.getMyAvailability(range),
    enabled: Boolean(range),
  });
}

// Müsaitlik değişince planlama penceresindeki takvimler de tazelenir.
function useAvailabilityMutation(mutationFn) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: availabilityKeys.all }),
        queryClient.invalidateQueries({ queryKey: interviewKeys.all }),
      ]),
  });
}

export const useAddAvailability = () => useAvailabilityMutation(api.addAvailability);

export const useDeleteAvailability = () => useAvailabilityMutation(api.deleteAvailability);
