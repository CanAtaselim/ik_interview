using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Features.Notifications;

/// <summary>
/// Başlangıç e-posta şablonları. Veritabanına bir kez eklenir; sonra yönetim ekranından
/// değiştirilir. Tabloda şablon yoksa (silinmiş veya pasif) gönderim bu metinlerle yapılır.
/// </summary>
public static class DefaultEmailTemplates
{
    public sealed record Definition(string Code, string Name, string Subject, string Body, string Variables);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(
            EmailTemplateCodes.Notification,
            "Uygulama bildirimi (çalışanlara)",
            "{Baslik}",
            """
            <p>Merhaba {AdSoyad},</p>
            <p><strong>{Baslik}</strong></p>
            <p>{Mesaj}</p>
            <p><a href="{Baglanti}">Uygulamada aç</a></p>
            """,
            "{AdSoyad}, {Baslik}, {Mesaj}, {Baglanti}"),
        new(
            EmailTemplateCodes.InterviewInvitationCandidate,
            "Mülakat daveti (adaya)",
            "Mülakat daveti: {Pozisyon}",
            """
            <p>Merhaba {AdayAdi},</p>
            <p><strong>{Pozisyon}</strong> pozisyonu için sizi mülakata davet ediyoruz.</p>
            <ul>
              <li>Tarih ve saat: {MulakatTarihi}</li>
              <li>Süre: {Sure}</li>
              <li>Yer: {Yer}</li>
            </ul>
            <p>Takviminize eklemek için ekteki daveti kullanabilirsiniz. Görüşmek üzere.</p>
            """,
            "{AdayAdi}, {Pozisyon}, {MulakatTarihi}, {Sure}, {Yer}, {MulakatTipi}"),
        new(
            EmailTemplateCodes.InterviewUpdatedCandidate,
            "Mülakat değişikliği (adaya)",
            "Mülakat bilgileriniz güncellendi: {Pozisyon}",
            """
            <p>Merhaba {AdayAdi},</p>
            <p><strong>{Pozisyon}</strong> pozisyonu için mülakatınızın bilgileri güncellendi.</p>
            <ul>
              <li>Tarih ve saat: {MulakatTarihi}</li>
              <li>Süre: {Sure}</li>
              <li>Yer: {Yer}</li>
            </ul>
            <p>Ekteki davet takviminizdeki kaydı günceller.</p>
            """,
            "{AdayAdi}, {Pozisyon}, {MulakatTarihi}, {Sure}, {Yer}, {MulakatTipi}"),
        new(
            EmailTemplateCodes.InterviewCancelledCandidate,
            "Mülakat iptali (adaya)",
            "Mülakat iptal edildi: {Pozisyon}",
            """
            <p>Merhaba {AdayAdi},</p>
            <p><strong>{MulakatTarihi}</strong> tarihli <strong>{Pozisyon}</strong> mülakatınız iptal edildi.</p>
            <p>Yeni bir görüşme planlandığında sizinle iletişime geçeceğiz.</p>
            """,
            "{AdayAdi}, {Pozisyon}, {MulakatTarihi}, {MulakatTipi}"),
    ];

    public static Definition? Find(string code) => All.FirstOrDefault(t => t.Code == code);
}
