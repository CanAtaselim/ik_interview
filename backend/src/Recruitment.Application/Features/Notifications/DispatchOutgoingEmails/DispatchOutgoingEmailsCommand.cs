using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Features.Notifications.DispatchOutgoingEmails;

/// <summary>
/// Kuyruktaki e-postaları gönderir; başarısız olanı sonraki çalıştırmada yeniden dener. Dakikada bir
/// zamanlanmış işten çağrılır. SMTP ayarlı değilse e-postalar gönderilmeden kayda geçer.
/// </summary>
public sealed record DispatchOutgoingEmailsCommand : ICommand<int>
{
    public const int BatchSize = 20;
}

internal sealed class DispatchOutgoingEmailsCommandHandler(
    IAppDbContext db, IEmailSender sender, TimeProvider clock, ILogger<DispatchOutgoingEmailsCommandHandler> logger)
    : ICommandHandler<DispatchOutgoingEmailsCommand, int>
{
    public async Task<Result<int>> Handle(DispatchOutgoingEmailsCommand command, CancellationToken ct)
    {
        var pending = await db.OutgoingEmails
            .Where(e => e.Status == OutgoingEmailStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(DispatchOutgoingEmailsCommand.BatchSize)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var email in pending)
        {
            if (!sender.IsConfigured)
            {
                email.MarkSkipped("SMTP ayarlanmadığı için gönderilmedi.");
                logger.LogInformation("SMTP ayarlı değil; e-posta gönderilmedi: {Subject} → {To}", email.Subject, email.ToAddress);
                continue;
            }

            try
            {
                await sender.Send(
                    new EmailEnvelope(email.ToAddress, email.ToName, email.Subject, email.HtmlBody, email.CalendarContent, email.CalendarMethod),
                    ct);
                email.MarkSent(clock.GetUtcNow());
                sent++;

                // Adaya giden her e-posta adayın geçmişine yazılır.
                if (email.CandidateId is { } candidateId)
                {
                    db.TimelineEntries.Add(TimelineEntry.Create(
                        candidateId, TimelineEntryType.EmailSent, $"E-posta gönderildi: {email.Subject}", null, clock.GetUtcNow()));
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                email.MarkFailed(exception.Message);
                logger.LogWarning(exception, "E-posta gönderilemedi ({Attempt}. deneme): {Subject} → {To}",
                    email.Attempts, email.Subject, email.ToAddress);
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }
}
