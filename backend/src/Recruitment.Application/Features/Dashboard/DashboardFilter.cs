using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Dashboard;

/// <summary>
/// Dashboard kartlarının ortak filtresi. Tarihler Türkiye saatiyle gün olarak verilir, iki uç da dahildir.
/// <paramref name="DepartmentId"/> ve <paramref name="PositionId"/> yalnızca birime/pozisyona bağlanabilen
/// kartları (talep, pozisyon, süreç, mülakat) süzer; aday havuzu gibi kartlar tüm kayıtları gösterir.
/// </summary>
public sealed record DashboardFilter(DateOnly? From, DateOnly? To, Guid? DepartmentId, Guid? PositionId)
{
    public const int DefaultDays = 90;
    public const int MaxDays = 366;
}

/// <summary>Çözümlenmiş dönem: [Start, End) aralığı ve aynı uzunlukta bir önceki dönem.</summary>
public sealed record DashboardPeriod(DateTimeOffset Start, DateTimeOffset End, DateTimeOffset PreviousStart, int Days)
{
    public bool Contains(DateTimeOffset value) => value >= Start && value < End;

    public bool PreviousContains(DateTimeOffset value) => value >= PreviousStart && value < Start;

    /// <summary>Dönem uzunluğuna göre grafik adımı: kısa dönemde gün, orta dönemde hafta, uzunda ay.</summary>
    public string Granularity => Days <= 31 ? "day" : Days <= 180 ? "week" : "month";

    /// <summary>Zamanı, grafik adımının başlangıç gününe (Türkiye saati) yuvarlar.</summary>
    public DateOnly Bucket(DateTimeOffset value)
    {
        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, TurkishTime.Zone).DateTime);
        return Granularity switch
        {
            "day" => local,
            "week" => local.AddDays(-(((int)local.DayOfWeek + 6) % 7)),
            _ => new DateOnly(local.Year, local.Month, 1),
        };
    }

    /// <summary>Dönemin tüm adımları; boş adımlar grafikte 0 olarak görünsün diye.</summary>
    public IEnumerable<DateOnly> Buckets()
    {
        var first = Bucket(Start);
        var last = Bucket(End.AddTicks(-1));
        for (var day = first; day <= last; day = Granularity switch
        {
            "day" => day.AddDays(1),
            "week" => day.AddDays(7),
            _ => day.AddMonths(1),
        })
            yield return day;
    }

    public static Result<DashboardPeriod> Resolve(DashboardFilter filter, TimeProvider clock)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TurkishTime.Zone).DateTime);
        var to = filter.To ?? today;
        var from = filter.From ?? to.AddDays(-(DashboardFilter.DefaultDays - 1));

        var errors = new Dictionary<string, string[]>();
        if (from > to)
            errors["from"] = ["Başlangıç tarihi bitiş tarihinden sonra olamaz."];
        else if (to.DayNumber - from.DayNumber + 1 > DashboardFilter.MaxDays)
            errors["from"] = [$"Tarih aralığı en fazla {DashboardFilter.MaxDays} gün olabilir."];
        if (errors.Count > 0)
            return Error.Validation(errors);

        var days = to.DayNumber - from.DayNumber + 1;
        var start = StartOfDay(from);
        return new DashboardPeriod(start, StartOfDay(to.AddDays(1)), StartOfDay(from.AddDays(-days)), days);
    }

    private static DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TurkishTime.Zone.GetUtcOffset(local));
    }
}

/// <summary>Bir grafik dilimi: ad ve değer (ör. kaynak adı ve aday sayısı).</summary>
public sealed record CountItem(string Label, int Count);

/// <summary>Zaman serisinde bir nokta; <paramref name="Date"/> adımın başlangıç günüdür.</summary>
public sealed record TimePoint(DateOnly Date, int Count);

/// <summary>Bir göstergenin bu dönemdeki ve bir önceki dönemdeki değeri (eğilim oku için).</summary>
public sealed record Kpi(int Value, int Previous);
