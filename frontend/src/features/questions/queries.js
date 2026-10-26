import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '@/shared/api/questionBank';
import { templateKeys } from '@/shared/recruitment/queries';

export const questionKeys = {
  all: ['questions'],
  list: (filters) => ['questions', 'list', filters],
  detail: (id) => ['questions', 'detail', id],
};

/** @param {object} filters */
export function useQuestions(filters) {
  return useQuery({
    queryKey: questionKeys.list(filters),
    queryFn: () => api.getQuestions(filters),
    placeholderData: keepPreviousData,
  });
}

/** @param {string | null} id */
export function useQuestion(id) {
  return useQuery({
    queryKey: questionKeys.detail(id),
    queryFn: () => api.getQuestion(id),
    enabled: Boolean(id),
  });
}

/** @param {string | null} id */
export function useInterviewTemplate(id) {
  return useQuery({
    queryKey: templateKeys.detail(id),
    queryFn: () => api.getInterviewTemplate(id),
    enabled: Boolean(id),
  });
}

// Soru ve şablonlar birbirini etkiler (kullanım sayısı, şablondaki soru metni); birlikte tazelenir.
function useQuestionBankMutation(mutationFn) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: questionKeys.all }),
        queryClient.invalidateQueries({ queryKey: templateKeys.all }),
      ]),
  });
}

export const useCreateQuestion = () => useQuestionBankMutation(api.createQuestion);
export const useUpdateQuestion = () => useQuestionBankMutation(api.updateQuestion);
export const useSetQuestionActive = () => useQuestionBankMutation(api.setQuestionActive);
export const useDeleteQuestion = () => useQuestionBankMutation(api.deleteQuestion);
export const useCreateInterviewTemplate = () =>
  useQuestionBankMutation(api.createInterviewTemplate);
export const useUpdateInterviewTemplate = () =>
  useQuestionBankMutation(api.updateInterviewTemplate);
export const useSetInterviewTemplateActive = () =>
  useQuestionBankMutation(api.setInterviewTemplateActive);
