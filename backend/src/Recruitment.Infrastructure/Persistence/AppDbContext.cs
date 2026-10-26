using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Approvals;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Calendar;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Common;
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
using Recruitment.Infrastructure.Outbox;

namespace Recruitment.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RoleGroupMapping> RoleGroupMappings => Set<RoleGroupMapping>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();
    public DbSet<ApprovalFlowDefinition> ApprovalFlowDefinitions => Set<ApprovalFlowDefinition>();
    public DbSet<HiringRequest> HiringRequests => Set<HiringRequest>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<PipelineTemplate> PipelineTemplates => Set<PipelineTemplate>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();
    public DbSet<CvReview> CvReviews => Set<CvReview>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewerAvailability> InterviewerAvailabilities => Set<InterviewerAvailability>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<InterviewTemplate> InterviewTemplates => Set<InterviewTemplate>();
    public DbSet<Scorecard> Scorecards => Set<Scorecard>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<OutgoingEmail> OutgoingEmails => Set<OutgoingEmail>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    public async Task<T> ExecuteInTransaction<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null)
            return await action();

        await using var transaction = await Database.BeginTransactionAsync(ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }

    // İstemciden gelen zaman herhangi bir saat diliminde olabilir ("2026-10-08T11:30:00+03:00");
    // veritabanına her zaman UTC yazılır (PostgreSQL sürücüsü başka ofset kabul etmez).
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Kara listede ad benzerliği araması için.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && typeof(Entity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in entityTypes)
        {
            var entity = modelBuilder.Entity(clrType);

            entity.Ignore(nameof(Entity.DomainEvents));

            // Kimlik uygulamada üretilir. Bu belirtilmezse EF, izlenen bir kaydın koleksiyonuna eklenen
            // yeni alt kaydı (kimliği dolu olduğu için) "zaten var" sayıp INSERT yerine UPDATE dener.
            entity.Property(nameof(Entity.Id)).ValueGeneratedNever();
            entity.Property(nameof(Entity.CreatedBy)).HasMaxLength(256);
            entity.Property(nameof(Entity.UpdatedBy)).HasMaxLength(256);
            entity.Property(nameof(Entity.DeletedBy)).HasMaxLength(256);

            // Soft delete: silinmiş kayıtlar her sorguda gizlenir.
            var parameter = Expression.Parameter(clrType, "e");
            var notDeleted = Expression.Not(Expression.Property(parameter, nameof(Entity.IsDeleted)));
            entity.HasQueryFilter(Expression.Lambda(notDeleted, parameter));
        }
    }

    private sealed class UtcDateTimeOffsetConverter()
        : ValueConverter<DateTimeOffset, DateTimeOffset>(value => value.ToUniversalTime(), value => value);
}
