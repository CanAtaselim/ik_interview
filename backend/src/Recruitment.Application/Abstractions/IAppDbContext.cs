using Microsoft.EntityFrameworkCore;
using Recruitment.Domain.Approvals;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Calendar;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;
using Recruitment.Domain.Questions;
using Recruitment.Domain.Scorecards;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RoleGroupMapping> RoleGroupMappings { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<DictionaryItem> DictionaryItems { get; }
    DbSet<ApprovalFlowDefinition> ApprovalFlowDefinitions { get; }
    DbSet<HiringRequest> HiringRequests { get; }
    DbSet<Position> Positions { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<TimelineEntry> TimelineEntries { get; }
    DbSet<AccessLog> AccessLogs { get; }
    DbSet<PipelineTemplate> PipelineTemplates { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<BlacklistEntry> BlacklistEntries { get; }
    DbSet<CvReview> CvReviews { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<Interview> Interviews { get; }
    DbSet<InterviewerAvailability> InterviewerAvailabilities { get; }
    DbSet<Question> Questions { get; }
    DbSet<InterviewTemplate> InterviewTemplates { get; }
    DbSet<Scorecard> Scorecards { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<OutgoingEmail> OutgoingEmails { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// <paramref name="action"/>'ı tek transaction içinde çalıştırır; exception fırlarsa geri alır.
    /// Açık bir transaction varsa ona katılır.
    /// </summary>
    Task<T> ExecuteInTransaction<T>(Func<Task<T>> action, CancellationToken ct = default);
}
