namespace Recruitment.Domain.Notifications;

public enum OutgoingEmailStatus
{
    Pending,
    Sent,

    /// <summary>Denemeler tükendi.</summary>
    Failed,

    /// <summary>SMTP ayarlanmadığı için gönderilmedi (yalnızca kayda geçti).</summary>
    Skipped,
}

public enum CalendarMethod
{
    Request,
    Cancel,
}

/// <summary>
/// Gönderilecek e-posta kuyruğu. Olay işlenirken kuyruğa yazılır, zamanlanmış iş gönderir ve
/// hata olursa birkaç kez yeniden dener; böylece SMTP kesintisi olayların işlenmesini bozmaz.
/// </summary>
public sealed class OutgoingEmail
{
    public const int MaxAttempts = 5;

    private OutgoingEmail() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string ToAddress { get; private set; } = default!;
    public string? ToName { get; private set; }
    public string Subject { get; private set; } = default!;
    public string HtmlBody { get; private set; } = default!;

    /// <summary>Mülakat daveti gibi e-postalarda eklenen takvim içeriği (.ics).</summary>
    public string? CalendarContent { get; private set; }

    public CalendarMethod? CalendarMethod { get; private set; }

    /// <summary>Adaya giden e-postada aday; gönderilince adayın zaman çizelgesine yazılır.</summary>
    public Guid? CandidateId { get; private set; }

    public string DedupeKey { get; private set; } = default!;
    public OutgoingEmailStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public static OutgoingEmail Create(
        string toAddress,
        string? toName,
        string subject,
        string htmlBody,
        string dedupeKey,
        DateTimeOffset now,
        Guid? candidateId = null,
        string? calendarContent = null,
        CalendarMethod? calendarMethod = null) =>
        new()
        {
            ToAddress = toAddress.Trim(),
            ToName = toName,
            Subject = subject,
            HtmlBody = htmlBody,
            DedupeKey = dedupeKey,
            CreatedAt = now,
            CandidateId = candidateId,
            CalendarContent = calendarContent,
            CalendarMethod = calendarMethod,
            Status = OutgoingEmailStatus.Pending,
        };

    public void MarkSent(DateTimeOffset now)
    {
        Attempts++;
        Status = OutgoingEmailStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        if (Attempts >= MaxAttempts)
            Status = OutgoingEmailStatus.Failed;
    }

    public void MarkSkipped(string reason)
    {
        Status = OutgoingEmailStatus.Skipped;
        LastError = reason;
    }
}
