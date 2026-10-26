import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '@/shared/api/settings';

export const settingsKeys = {
  emailTemplates: ['settings', 'email-templates'],
  approvalFlow: ['settings', 'approval-flow'],
  pipelineStages: ['settings', 'pipeline-stages'],
  holidays: (year) => ['settings', 'holidays', year],
  holidaysAll: ['settings', 'holidays'],
  roleMappings: ['settings', 'role-mappings'],
};

export const useEmailTemplates = () =>
  useQuery({ queryKey: settingsKeys.emailTemplates, queryFn: api.getEmailTemplates });

export const useApprovalFlow = () =>
  useQuery({ queryKey: settingsKeys.approvalFlow, queryFn: api.getApprovalFlow });

export const usePipelineStages = () =>
  useQuery({ queryKey: settingsKeys.pipelineStages, queryFn: api.getPipelineStages });

/** @param {number} year */
export const useHolidays = (year) =>
  useQuery({ queryKey: settingsKeys.holidays(year), queryFn: () => api.getHolidays(year) });

export const useRoleMappings = () =>
  useQuery({ queryKey: settingsKeys.roleMappings, queryFn: api.getRoleMappings });

function useSettingsMutation(mutationFn, queryKey) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn, onSuccess: () => queryClient.invalidateQueries({ queryKey }) });
}

export const useUpdateEmailTemplate = () =>
  useSettingsMutation(api.updateEmailTemplate, settingsKeys.emailTemplates);
export const useResetEmailTemplate = () =>
  useSettingsMutation(api.resetEmailTemplate, settingsKeys.emailTemplates);
export const usePreviewEmailTemplate = () => useMutation({ mutationFn: api.previewEmailTemplate });
export const useUpdateApprovalFlow = () =>
  useSettingsMutation(api.updateApprovalFlow, settingsKeys.approvalFlow);
export const useUpdatePipelineStages = () =>
  useSettingsMutation(api.updatePipelineStages, ['settings', 'pipeline-stages']);
export const useCreateHoliday = () =>
  useSettingsMutation(api.createHoliday, settingsKeys.holidaysAll);
export const useDeleteHoliday = () =>
  useSettingsMutation(api.deleteHoliday, settingsKeys.holidaysAll);
export const useCreateRoleMapping = () =>
  useSettingsMutation(api.createRoleMapping, settingsKeys.roleMappings);
export const useDeleteRoleMapping = () =>
  useSettingsMutation(api.deleteRoleMapping, settingsKeys.roleMappings);
