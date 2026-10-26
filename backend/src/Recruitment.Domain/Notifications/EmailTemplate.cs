using Recruitment.Domain.Common;

namespace Recruitment.Domain.Notifications;

/// <summary>
/// E-posta şablonu. Konu ve gövdedeki <c>{Degisken}</c> alanları gönderimde doldurulur. Yönetim
/// ekranından düzenlenir; kod sabittir.
/// </summary>
public sealed class EmailTemplate : Entity
{
    public const int SubjectMaxLength = 300;

    private EmailTemplate() { }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;

    /// <summary>Şablonda kullanılabilecek değişkenler (yönetim ekranında yol gösterir).</summary>
    public string Variables { get; private set; } = default!;

    public static EmailTemplate Create(string code, string name, string subject, string body, string variables) =>
        new() { Code = code, Name = name, Subject = subject, Body = body, Variables = variables };

    public void Update(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("E-posta konusunu yazmalısınız.");
        if (string.IsNullOrWhiteSpace(body))
            throw new DomainException("E-posta içeriğini yazmalısınız.");

        Subject = subject.Trim();
        Body = body.Trim();
    }
}

/// <summary>Kodda kullanılan şablon kodları.</summary>
public static class EmailTemplateCodes
{
    public const string Notification = "Notification";
    public const string InterviewInvitationCandidate = "InterviewInvitationCandidate";
    public const string InterviewUpdatedCandidate = "InterviewUpdatedCandidate";
    public const string InterviewCancelledCandidate = "InterviewCancelledCandidate";
}
