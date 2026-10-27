import { createBrowserRouter, Navigate } from 'react-router';
import ApprovalFlowPage from '@/features/admin/pages/ApprovalFlowPage';
import DictionariesPage from '@/features/admin/pages/DictionariesPage';
import EmailTemplatesPage from '@/features/admin/pages/EmailTemplatesPage';
import HolidaysPage from '@/features/admin/pages/HolidaysPage';
import PipelineStagesPage from '@/features/admin/pages/PipelineStagesPage';
import RoleMappingsPage from '@/features/admin/pages/RoleMappingsPage';
import LoginPage from '@/features/auth/pages/LoginPage';
import BlacklistPage from '@/features/blacklist/pages/BlacklistPage';
import CandidateDetailPage from '@/features/candidates/pages/CandidateDetailPage';
import CandidateListPage from '@/features/candidates/pages/CandidateListPage';
import CvReviewDetailPage from '@/features/cv-reviews/pages/CvReviewDetailPage';
import CvReviewListPage from '@/features/cv-reviews/pages/CvReviewListPage';
import DashboardPage from '@/features/dashboard/pages/DashboardPage';
import MyTasksPage from '@/features/dashboard/pages/MyTasksPage';
import NotificationSettingsPage from '@/features/notifications/pages/NotificationSettingsPage';
import AvailabilityPage from '@/features/interviews/pages/AvailabilityPage';
import InterviewCalendarPage from '@/features/interviews/pages/InterviewCalendarPage';
import InterviewDetailPage from '@/features/interviews/pages/InterviewDetailPage';
import PipelinePage from '@/features/pipeline/pages/PipelinePage';
import InterviewTemplatesPage from '@/features/questions/pages/InterviewTemplatesPage';
import ComparisonPage from '@/features/scorecards/pages/ComparisonPage';
import ScorecardPage from '@/features/scorecards/pages/ScorecardPage';
import QuestionBankPage from '@/features/questions/pages/QuestionBankPage';
import HiringRequestListPage from '@/features/hiring-requests/pages/HiringRequestListPage';
import PositionDetailPage from '@/features/positions/pages/PositionDetailPage';
import PositionListPage from '@/features/positions/pages/PositionListPage';
import RequireAuth from '@/shared/auth/RequireAuth';
import RequireRole from '@/shared/auth/RequireRole';
import {
  HR_ROLES,
  INTERVIEWER_ROLES,
  QUESTION_ROLES,
  Role,
  SETTINGS_ROLES,
} from '@/shared/auth/roles';
import { bootstrapSession } from '@/shared/auth/session';
import { ROUTES } from '@/shared/constants/routes';
import AppLayout from '@/shared/layout/AppLayout';
import AppSkeleton from '@/shared/layout/AppSkeleton';
import ErrorPage from '@/shared/pages/ErrorPage';
import NotFoundPage from '@/shared/pages/NotFoundPage';

/**
 * Oturum gerektiren rotalar. `handle.roles` boşsa oturum açmış herkes girebilir;
 * `handle.menu` tanımlıysa rota sol menüde görünür. Menü bu listeden üretilir.
 */
const RECRUITMENT_ROLES = [
  Role.TeamLead,
  Role.DepartmentManager,
  Role.HrSpecialist,
  Role.HrManager,
];

const CV_REVIEW_ROLES = [Role.TeamLead, ...HR_ROLES];

const appRoutes = [
  {
    path: ROUTES.home,
    element: <MyTasksPage />,
    handle: { roles: null, menu: { label: 'Bekleyen işlerim', icon: 'pi pi-inbox' } },
  },
  {
    path: ROUTES.dashboard,
    element: <DashboardPage />,
    handle: { roles: [Role.HrManager], menu: { label: 'Dashboard', icon: 'pi pi-chart-bar' } },
  },
  {
    path: ROUTES.candidates,
    element: <CandidateListPage />,
    handle: { roles: HR_ROLES, menu: { label: 'Aday Listesi', icon: 'pi pi-users' } },
  },
  {
    path: ROUTES.candidate(':id'),
    element: <CandidateDetailPage />,
    handle: { roles: HR_ROLES },
  },
  {
    path: ROUTES.cvReviews,
    element: <CvReviewListPage />,
    handle: {
      roles: CV_REVIEW_ROLES,
      menu: { label: 'CV değerlendirmeleri', icon: 'pi pi-id-card' },
    },
  },
  {
    path: ROUTES.cvReview(':id'),
    element: <CvReviewDetailPage />,
    handle: { roles: CV_REVIEW_ROLES },
  },
  {
    path: ROUTES.interviews,
    element: <InterviewCalendarPage />,
    handle: { roles: INTERVIEWER_ROLES, menu: { label: 'Mülakatlar', icon: 'pi pi-calendar' } },
  },
  {
    path: ROUTES.interview(':id'),
    element: <InterviewDetailPage />,
    handle: { roles: INTERVIEWER_ROLES },
  },
  {
    path: ROUTES.scorecard(':id'),
    element: <ScorecardPage />,
    handle: { roles: INTERVIEWER_ROLES },
  },
  {
    path: ROUTES.availability,
    element: <AvailabilityPage />,
    handle: { roles: INTERVIEWER_ROLES, menu: { label: 'Müsaitliğim', icon: 'pi pi-clock' } },
  },
  {
    path: ROUTES.questions,
    element: <QuestionBankPage />,
    handle: {
      roles: QUESTION_ROLES,
      menu: { label: 'Soru bankası', icon: 'pi pi-question-circle' },
    },
  },
  {
    path: ROUTES.interviewTemplates,
    element: <InterviewTemplatesPage />,
    handle: { roles: QUESTION_ROLES, menu: { label: 'Mülakat şablonları', icon: 'pi pi-list' } },
  },
  {
    path: ROUTES.hiringRequests,
    element: <HiringRequestListPage />,
    handle: {
      roles: RECRUITMENT_ROLES,
      menu: { label: 'Kadro talepleri', icon: 'pi pi-file-edit' },
    },
  },
  {
    path: ROUTES.positions,
    element: <PositionListPage />,
    handle: { roles: RECRUITMENT_ROLES, menu: { label: 'Pozisyonlar', icon: 'pi pi-briefcase' } },
  },
  {
    path: ROUTES.position(':id'),
    element: <PositionDetailPage />,
    handle: { roles: RECRUITMENT_ROLES },
  },
  {
    path: ROUTES.comparison(':id'),
    element: <ComparisonPage />,
    handle: { roles: RECRUITMENT_ROLES },
  },
  {
    path: ROUTES.pipeline(':id'),
    element: <PipelinePage />,
    handle: { roles: HR_ROLES },
  },
  {
    path: ROUTES.blacklist,
    element: <BlacklistPage />,
    handle: {
      roles: [...HR_ROLES, Role.SystemAdmin],
      menu: { label: 'Kara liste', icon: 'pi pi-ban' },
    },
  },
  {
    path: ROUTES.notificationSettings,
    element: <NotificationSettingsPage />,
    handle: { roles: null },
  },
  {
    // Yönetim menüsü tek öğedir; sayfalar arasında sekme şeridiyle gezilir.
    path: ROUTES.admin,
    element: <Navigate to={ROUTES.dictionaries} replace />,
    handle: { roles: SETTINGS_ROLES, menu: { label: 'Yönetim', icon: 'pi pi-cog' } },
  },
  {
    path: ROUTES.dictionaries,
    element: <DictionariesPage />,
    handle: { roles: SETTINGS_ROLES },
  },
  { path: ROUTES.approvalFlow, element: <ApprovalFlowPage />, handle: { roles: SETTINGS_ROLES } },
  {
    path: ROUTES.pipelineStages,
    element: <PipelineStagesPage />,
    handle: { roles: SETTINGS_ROLES },
  },
  {
    path: ROUTES.emailTemplates,
    element: <EmailTemplatesPage />,
    handle: { roles: SETTINGS_ROLES },
  },
  { path: ROUTES.holidays, element: <HolidaysPage />, handle: { roles: SETTINGS_ROLES } },
  {
    path: ROUTES.roleMappings,
    element: <RoleMappingsPage />,
    handle: { roles: [Role.SystemAdmin] },
  },
];

const navigation = appRoutes
  .filter((route) => route.handle.menu)
  .map((route) => ({ path: route.path, roles: route.handle.roles, ...route.handle.menu }));

export const router = createBrowserRouter([
  {
    // Oturum geri yüklenmeden hiçbir sayfa render edilmez.
    loader: bootstrapSession,
    shouldRevalidate: () => false,
    HydrateFallback: AppSkeleton,
    ErrorBoundary: ErrorPage,
    children: [
      { path: ROUTES.login, element: <LoginPage /> },
      {
        element: (
          <RequireAuth>
            <AppLayout navigation={navigation} />
          </RequireAuth>
        ),
        children: [
          ...appRoutes.map((route) => ({
            ...route,
            element: <RequireRole roles={route.handle.roles}>{route.element}</RequireRole>,
          })),
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
