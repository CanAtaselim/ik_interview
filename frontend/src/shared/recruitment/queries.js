import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as applications from '@/shared/api/applications';
import * as blacklist from '@/shared/api/blacklist';
import * as cvReviews from '@/shared/api/cvReviews';
import * as interviews from '@/shared/api/interviews';
import * as questionBank from '@/shared/api/questionBank';
import * as scorecards from '@/shared/api/scorecards';
import { searchUsers } from '@/shared/api/users';
import { INTERVIEWER_ROLES, Role } from '@/shared/auth/roles';

export const pipelineKeys = {
  all: ['pipeline'],
  board: (positionId) => ['pipeline', 'board', positionId],
  candidate: (candidateId) => ['pipeline', 'candidate', candidateId],
};

export const blacklistKeys = {
  all: ['blacklist'],
  list: (filters) => ['blacklist', 'list', filters],
  detail: (id) => ['blacklist', 'detail', id],
};

export const cvReviewKeys = {
  all: ['cv-reviews'],
  list: (filters) => ['cv-reviews', 'list', filters],
  detail: (id) => ['cv-reviews', 'detail', id],
};

export const interviewKeys = {
  all: ['interviews'],
  list: (filters) => ['interviews', 'list', filters],
  detail: (id) => ['interviews', 'detail', id],
  schedule: (params) => ['interviews', 'schedule', params],
};

export const scorecardKeys = {
  all: ['scorecards'],
  detail: (id) => ['scorecards', 'detail', id],
  interview: (interviewId) => ['scorecards', 'interview', interviewId],
  comparison: (positionId) => ['scorecards', 'comparison', positionId],
};

export const templateKeys = {
  all: ['interview-templates'],
  list: (filters) => ['interview-templates', 'list', filters],
  detail: (id) => ['interview-templates', 'detail', id],
};

// Süreçteki bir değişiklik kanbanı, aday kartını, zaman çizelgesini ve pozisyon doluluğunu etkiler.
function useInvalidateRecruitment() {
  const queryClient = useQueryClient();

  return () =>
    Promise.all(
      [
        pipelineKeys.all,
        blacklistKeys.all,
        cvReviewKeys.all,
        interviewKeys.all,
        scorecardKeys.all,
        ['candidates'],
        ['positions'],
      ].map((queryKey) => queryClient.invalidateQueries({ queryKey })),
    );
}

/** @param {string} positionId */
export function usePositionPipeline(positionId) {
  return useQuery({
    queryKey: pipelineKeys.board(positionId),
    queryFn: () => applications.getPositionPipeline(positionId),
  });
}

/** @param {string} candidateId */
export function useCandidateApplications(candidateId) {
  return useQuery({
    queryKey: pipelineKeys.candidate(candidateId),
    queryFn: () => applications.getCandidateApplications(candidateId),
  });
}

export function useOpenPositions() {
  return useQuery({ queryKey: ['positions', 'open'], queryFn: applications.getOpenPositions });
}

function useRecruitmentMutation(mutationFn) {
  const invalidate = useInvalidateRecruitment();

  return useMutation({ mutationFn, onSuccess: invalidate });
}

export const useStartApplication = () => useRecruitmentMutation(applications.startApplication);

export const useCloseApplication = () => useRecruitmentMutation(applications.closeApplication);

export const useUndoApplicationChange = () =>
  useRecruitmentMutation(applications.undoApplicationChange);

/**
 * Kanbanda kart taşıma. Kart hemen yeni kolonda görünür (iyimser güncelleme); sunucu geçişi
 * reddederse eski yerine döner.
 * @param {string} positionId
 */
export function useMoveApplication(positionId) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateRecruitment();
  const queryKey = pipelineKeys.board(positionId);

  return useMutation({
    mutationFn: applications.moveApplication,
    onMutate: async ({ applicationId, stageId }) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData(queryKey);
      queryClient.setQueryData(
        queryKey,
        (board) =>
          board && {
            ...board,
            active: board.active.map((card) =>
              card.applicationId === applicationId
                ? { ...card, stageId, stageEnteredAt: new Date().toISOString() }
                : card,
            ),
          },
      );
      return { previous };
    },
    onError: (_error, _variables, context) => queryClient.setQueryData(queryKey, context.previous),
    onSettled: invalidate,
  });
}

/** @param {object} filters */
export function useBlacklistEntries(filters) {
  return useQuery({
    queryKey: blacklistKeys.list(filters),
    queryFn: () => blacklist.getBlacklistEntries(filters),
    placeholderData: keepPreviousData,
  });
}

/** @param {string | null} id */
export function useBlacklistEntry(id) {
  return useQuery({
    queryKey: blacklistKeys.detail(id),
    queryFn: () => blacklist.getBlacklistEntry(id),
    enabled: Boolean(id),
  });
}

export const useRequestBlacklistEntry = () =>
  useRecruitmentMutation(blacklist.requestBlacklistEntry);

export const useApproveBlacklistEntry = () =>
  useRecruitmentMutation(blacklist.approveBlacklistEntry);

export const useRemoveBlacklistEntry = () => useRecruitmentMutation(blacklist.removeBlacklistEntry);

/**
 * @param {Parameters<typeof cvReviews.getCvReviews>[0]} filters
 * @param {{ enabled?: boolean }} [options]
 */
export function useCvReviews(filters, { enabled = true } = {}) {
  return useQuery({
    queryKey: cvReviewKeys.list(filters),
    queryFn: () => cvReviews.getCvReviews(filters),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/** @param {string} id */
export function useCvReview(id) {
  return useQuery({ queryKey: cvReviewKeys.detail(id), queryFn: () => cvReviews.getCvReview(id) });
}

/** CV önerilebilecek takım liderleri (uygulamaya en az bir kez giriş yapmış olanlar). */
export function useTeamLeads() {
  return useQuery({
    queryKey: ['users', 'select', [Role.TeamLead]],
    queryFn: () => searchUsers({ roles: [Role.TeamLead] }),
    staleTime: 60_000,
    select: (data) => data.items,
  });
}

export const useSuggestCandidate = () => useRecruitmentMutation(cvReviews.suggestCandidate);

export const useDecideCvReview = () => useRecruitmentMutation(cvReviews.decideCvReview);

/**
 * @param {Parameters<typeof interviews.getInterviews>[0]} filters
 * @param {{ enabled?: boolean }} [options]
 */
export function useInterviews(filters, { enabled = true } = {}) {
  return useQuery({
    queryKey: interviewKeys.list(filters),
    queryFn: () => interviews.getInterviews(filters),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/** @param {string} id */
export function useInterview(id) {
  return useQuery({
    queryKey: interviewKeys.detail(id),
    queryFn: () => interviews.getInterview(id),
  });
}

/**
 * Planlama penceresinde seçilen gün için mülakatçıların müsaitliği ve dolu saatleri.
 * @param {Parameters<typeof interviews.getPlanningSchedule>[0]} params
 * @param {{ enabled?: boolean }} [options]
 */
export function usePlanningSchedule(params, { enabled = true } = {}) {
  return useQuery({
    queryKey: interviewKeys.schedule(params),
    queryFn: () => interviews.getPlanningSchedule(params),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/** Mülakatçı olarak atanabilecek kullanıcılar. */
export function useInterviewers() {
  return useQuery({
    queryKey: ['users', 'select', INTERVIEWER_ROLES],
    queryFn: () => searchUsers({ roles: [...INTERVIEWER_ROLES] }),
    staleTime: 60_000,
    select: (data) => data.items,
  });
}

export const useScheduleInterview = () => useRecruitmentMutation(interviews.scheduleInterview);

export const useRescheduleInterview = () => useRecruitmentMutation(interviews.rescheduleInterview);

export const useCancelInterview = () => useRecruitmentMutation(interviews.cancelInterview);

export const useRecordInterviewOutcome = () =>
  useRecruitmentMutation(interviews.recordInterviewOutcome);

/**
 * Mülakat şablonları (soru bankası ekranı, pozisyon ve planlama seçimleri).
 * @param {Parameters<typeof questionBank.getInterviewTemplates>[0]} filters
 * @param {{ enabled?: boolean }} [options]
 */
export function useInterviewTemplates(filters, { enabled = true } = {}) {
  return useQuery({
    queryKey: templateKeys.list(filters),
    queryFn: () => questionBank.getInterviewTemplates(filters),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export const useSetPositionInterviewTemplates = () =>
  useRecruitmentMutation(questionBank.setPositionInterviewTemplates);

/** @param {string} interviewId */
export function useInterviewScorecards(interviewId) {
  return useQuery({
    queryKey: scorecardKeys.interview(interviewId),
    queryFn: () => scorecards.getInterviewScorecards(interviewId),
  });
}

/** @param {string | null} id */
export function useScorecard(id) {
  return useQuery({
    queryKey: scorecardKeys.detail(id),
    queryFn: () => scorecards.getScorecard(id),
    enabled: Boolean(id),
  });
}

/** @param {string} positionId */
export function usePositionComparison(positionId) {
  return useQuery({
    queryKey: scorecardKeys.comparison(positionId),
    queryFn: () => scorecards.getPositionComparison(positionId),
  });
}

export const useSubmitScorecard = () => useRecruitmentMutation(scorecards.submitScorecard);

export const useReopenScorecard = () => useRecruitmentMutation(scorecards.reopenScorecard);

export const useRecordDebrief = () => useRecruitmentMutation(scorecards.recordDebrief);
