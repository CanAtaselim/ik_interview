using System.Collections.Concurrent;
using Recruitment.Application.Abstractions;

namespace Recruitment.Api.IntegrationTests.Infrastructure;

/// <summary>Gönderilen e-postaları bellekte tutar (SMTP yerine).</summary>
public sealed class TestEmailSender : IEmailSender
{
    public ConcurrentBag<EmailEnvelope> Sent { get; } = [];

    public bool IsConfigured { get; set; } = true;

    public Task Send(EmailEnvelope email, CancellationToken ct)
    {
        Sent.Add(email);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailEnvelope> To(string address) => [.. Sent.Where(e => e.To == address)];
}

/// <summary>Anlık iletilen bildirimleri bellekte tutar (SignalR yerine).</summary>
public sealed class RecordingNotificationPusher : INotificationPusher
{
    public ConcurrentBag<(Guid UserId, NotificationDto Notification)> Pushed { get; } = [];

    public Task Push(Guid userId, NotificationDto notification, CancellationToken ct)
    {
        Pushed.Add((userId, notification));
        return Task.CompletedTask;
    }
}

/// <summary>Outlook yerine testin belirlediği meşgul saatleri döner. Varsayılan olarak kapalıdır.</summary>
public sealed class TestCalendarService : ICalendarService
{
    private readonly ConcurrentDictionary<string, MailboxBusy> _mailboxes = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled { get; set; }

    public void Set(string mailbox, MailboxBusy busy) => _mailboxes[mailbox] = busy;

    public Task<IReadOnlyDictionary<string, MailboxBusy>> GetBusy(
        IReadOnlyCollection<string> mailboxes, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, MailboxBusy>>(
            mailboxes.ToDictionary(m => m, m => _mailboxes.GetValueOrDefault(m) ?? new MailboxBusy(OutlookStatus.Ok, []), StringComparer.OrdinalIgnoreCase));
}
