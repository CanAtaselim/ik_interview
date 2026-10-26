namespace Recruitment.Application.Abstractions;

/// <summary>Bir posta kutusunun Outlook takvimindeki okunabilir durumu.</summary>
public enum OutlookStatus
{
    /// <summary>Outlook bağlantısı kurulmamış; yalnızca sistem içi bilgi kullanılır.</summary>
    NotConfigured,

    Ok,

    /// <summary>Kullanıcının e-posta adresi yok ya da Outlook'ta böyle bir posta kutusu bulunamadı.</summary>
    NoMailbox,

    /// <summary>Outlook'a o an ulaşılamadı veya yanıt alınamadı.</summary>
    Unavailable,
}

public sealed record BusySlot(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Bir posta kutusunun meşgul saatleri. Toplantı konusu/katılımcıları bilerek taşınmaz.</summary>
public sealed record MailboxBusy(OutlookStatus Status, IReadOnlyList<BusySlot> Busy)
{
    public static MailboxBusy Of(OutlookStatus status) => new(status, []);
}

/// <summary>
/// Outlook (Exchange Online) takviminden meşgul saatleri okur. Ulaşılamama planlamayı engellemez:
/// hata durumunda <see cref="OutlookStatus.Unavailable"/> döner, istisna fırlatmaz.
/// </summary>
public interface ICalendarService
{
    /// <summary>Bağlantı ayarları dolu mu?</summary>
    bool IsEnabled { get; }

    /// <summary>Anahtar: posta adresi (büyük/küçük harf duyarsız). Her istenen adres için bir kayıt döner.</summary>
    Task<IReadOnlyDictionary<string, MailboxBusy>> GetBusy(
        IReadOnlyCollection<string> mailboxes, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
