using Microsoft.EntityFrameworkCore;
using Recruitment.Domain.Approvals;
using Recruitment.Domain.Calendar;
using Recruitment.Application.Features.Notifications;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Seed;

/// <summary>Uygulamanın çalışması için gereken başlangıç verisi. Her açılışta çalışabilir (idempotent).</summary>
internal sealed class DatabaseSeeder(AppDbContext db)
{
    private static readonly Dictionary<DictionaryType, string[]> DefaultDictionaries = new()
    {
        // Kadro talebinde pozisyon adı bu listeden seçilir; yönetim ekranından eklenir. Kıdem unvanda,
        // aynı unvan içindeki basamak ise Seviye listesinde (L1, L2…) tutulur.
        [DictionaryType.PositionTitle] = ["Yazılım Geliştirme Uzmanı", "Kıdemli Yazılım Geliştirme Uzmanı"],
        [DictionaryType.Level] = ["L1", "L2", "L3", "L4"],
        // Ön görüşme formunda eğitim seviyesi bu listeden seçilir; yönetim ekranından düzenlenir.
        [DictionaryType.EducationLevel] = ["Ön Lisans", "Lisans", "Yüksek Lisans", "Doktora"],
        // "CV" adı özeldir: aday listesindeki CV simgesi bu türdeki (ve türü olmayan eski) dosyayı açar.
        [DictionaryType.DocumentType] = ["CV", "Sertifika", "Case çalışması", "Diğer"],
        [DictionaryType.InterviewType] = ["Telefon", "Teknik", "Case sunumu", "Yönetici", "Kültür uyumu"],
        // Soru bankasında yetkinlik zorunlu olduğu için başlangıç listesi; yönetim ekranından değiştirilebilir.
        [DictionaryType.Competency] =
        [
            "Teknik bilgi",
            "Problem çözme",
            "Sistem tasarımı",
            "İletişim",
            "Takım çalışması",
            "Sorumluluk alma",
            "Öğrenme isteği",
        ],
        // Süreç sonlandırırken neden zorunlu olduğu için boş bırakılamaz; yönetim ekranından değiştirilebilir.
        [DictionaryType.RejectionReason] =
        [
            "Teknik yetkinlik yetersiz",
            "Deneyim yetersiz",
            "Ücret beklentisi uyuşmadı",
            "Kültür uyumu",
            "Aday teklifi reddetti",
            "Aday süreçten çekildi",
            "Pozisyon kapatıldı",
            "Diğer",
        ],
        [DictionaryType.BlacklistReason] =
        [
            "Mülakata haber vermeden gelmedi",
            "Teklifi kabul edip işe başlamadı",
            "Yanlış/yanıltıcı bilgi verdi",
            "Etik olmayan davranış",
            "Disiplin nedeniyle ayrılmış eski çalışan",
            "Diğer",
        ],
    };

    // Her yıl aynı güne denk gelen resmi tatiller. Tarihi yıldan yıla değişen dini bayramlar
    // burada yoktur; o günler yıl belirtilerek tabloya eklenir.
    private static readonly (string Name, int Month, int Day)[] FixedHolidays =
    [
        ("Yılbaşı", 1, 1),
        ("Ulusal Egemenlik ve Çocuk Bayramı", 4, 23),
        ("Emek ve Dayanışma Günü", 5, 1),
        ("Atatürk'ü Anma, Gençlik ve Spor Bayramı", 5, 19),
        ("Demokrasi ve Millî Birlik Günü", 7, 15),
        ("Zafer Bayramı", 8, 30),
        ("Cumhuriyet Bayramı", 10, 29),
    ];

    // Varsayılan kadro talebi onay akışı: önce birimin yöneticisi, sonra İK yöneticisi.
    private static readonly Role[] DefaultHiringRequestApprovers = [Role.DepartmentManager, Role.HrManager];

    public async Task Seed(CancellationToken ct)
    {
        await SeedRoleGroupMappings(ct);
        await SeedDictionaries(ct);
        await SeedApprovalFlows(ct);
        await SeedPipelineTemplate(ct);
        await SeedHolidays(ct);
        await SeedEmailTemplates(ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Varsayılan AD grubu → rol eşlemesi: "Recruitment-&lt;Rol&gt;". Yalnızca tablo hiç
    /// doldurulmamışsa eklenir; admin'in sildiği eşleme sonraki açılışta geri gelmez.
    /// </summary>
    private async Task SeedRoleGroupMappings(CancellationToken ct)
    {
        if (await db.RoleGroupMappings.IgnoreQueryFilters().AnyAsync(ct))
            return;

        foreach (var role in Enum.GetValues<Role>())
            db.RoleGroupMappings.Add(RoleGroupMapping.Create(DefaultGroupName(role), role));
    }

    // Bir sözlük türü yalnızca hiç öğesi yokken doldurulur; admin'in düzenlediği liste ezilmez.
    private async Task SeedDictionaries(CancellationToken ct)
    {
        var seededTypes = await db.DictionaryItems.IgnoreQueryFilters()
            .Select(d => d.Type)
            .Distinct()
            .ToListAsync(ct);

        foreach (var (type, names) in DefaultDictionaries.Where(entry => !seededTypes.Contains(entry.Key)))
        {
            db.DictionaryItems.AddRange(names.Select((name, index) => DictionaryItem.Create(type, name, index + 1)));
        }
    }

    private async Task SeedApprovalFlows(CancellationToken ct)
    {
        var exists = await db.ApprovalFlowDefinitions.IgnoreQueryFilters()
            .AnyAsync(f => f.Kind == ApprovalFlowKind.HiringRequest, ct);
        if (exists)
            return;

        db.ApprovalFlowDefinitions.Add(
            ApprovalFlowDefinition.Create(ApprovalFlowKind.HiringRequest, DefaultHiringRequestApprovers));
    }

    private async Task SeedPipelineTemplate(CancellationToken ct)
    {
        if (await db.PipelineTemplates.IgnoreQueryFilters().AnyAsync(ct))
            return;

        db.PipelineTemplates.Add(PipelineTemplate.CreateDefault());
    }

    private async Task SeedHolidays(CancellationToken ct)
    {
        if (await db.Holidays.IgnoreQueryFilters().AnyAsync(ct))
            return;

        db.Holidays.AddRange(FixedHolidays.Select(h => Holiday.Create(h.Name, h.Month, h.Day)));
    }

    // Yeni eklenen şablon kodları her açılışta eklenir; düzenlenmiş şablonlara dokunulmaz.
    private async Task SeedEmailTemplates(CancellationToken ct)
    {
        var existing = await db.EmailTemplates.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct);
        db.EmailTemplates.AddRange(DefaultEmailTemplates.All
            .Where(t => !existing.Contains(t.Code))
            .Select(t => EmailTemplate.Create(t.Code, t.Name, t.Subject, t.Body.Trim(), t.Variables)));
    }

    internal static string DefaultGroupName(Role role) => $"Recruitment-{role}";
}
