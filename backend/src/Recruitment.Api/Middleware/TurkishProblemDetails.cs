namespace Recruitment.Api.Middleware;

/// <summary>
/// Çerçevenin kendi ürettiği hata yanıtlarını (yetkisiz, bulunamadı, okunamayan istek)
/// Türkçeleştirir. Uygulamanın ürettiği hatalar zaten Türkçe <c>detail</c> taşır, onlara dokunulmaz.
/// </summary>
internal static class TurkishProblemDetails
{
    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (problem.Detail is not null)
            return;

        (problem.Title, problem.Detail) = problem.Status switch
        {
            StatusCodes.Status400BadRequest => ("İstek geçersiz", "Gönderilen bilgiler okunamadı."),
            StatusCodes.Status401Unauthorized => ("Oturum gerekli", "Bu işlem için giriş yapmalısınız."),
            StatusCodes.Status403Forbidden => ("Yetkiniz yok", "Bu işlem için yetkiniz bulunmuyor."),
            StatusCodes.Status404NotFound => ("Bulunamadı", "Aradığınız kayıt veya sayfa bulunamadı."),
            StatusCodes.Status405MethodNotAllowed => ("İşlem desteklenmiyor", "Bu adres bu işlemi desteklemiyor."),
            StatusCodes.Status415UnsupportedMediaType => ("İstek geçersiz", "Gönderilen içerik türü desteklenmiyor."),
            _ => (problem.Title, problem.Detail),
        };
    }
}
