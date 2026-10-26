namespace Recruitment.Domain.Notifications;

/// <summary>
/// Kullanıcıya uygulama içinde gösterilen bildirim. Domain event handler'ları oluşturur; aynı olay
/// iki kez işlense de <see cref="DedupeKey"/> sayesinde tek bildirim oluşur.
/// </summary>
public sealed class Notification
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 1000;

    private Notification() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Body { get; private set; }

    /// <summary>Uygulama içi yol (ör. "/cv-reviews/…"); tıklanınca açılır.</summary>
    public string? Link { get; private set; }

    public string DedupeKey { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid recipientUserId, NotificationType type, string title, string? body, string? link, string dedupeKey, DateTimeOffset now) =>
        new()
        {
            RecipientUserId = recipientUserId,
            Type = type,
            Title = Truncate(title, TitleMaxLength)!,
            Body = Truncate(body, BodyMaxLength),
            Link = link,
            DedupeKey = dedupeKey,
            CreatedAt = now,
        };

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..(max - 1)] + "…";
}

/// <summary>Kullanıcının bir bildirim türü için e-posta tercihi. Kayıt yoksa e-posta gönderilir.</summary>
public sealed class NotificationPreference
{
    private NotificationPreference() { }

    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public bool EmailEnabled { get; private set; }

    public static NotificationPreference Create(Guid userId, NotificationType type, bool emailEnabled) =>
        new() { UserId = userId, Type = type, EmailEnabled = emailEnabled };

    public void SetEmail(bool enabled) => EmailEnabled = enabled;
}
