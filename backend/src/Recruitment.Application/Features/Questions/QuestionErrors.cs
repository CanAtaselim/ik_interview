using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Questions;

public static class QuestionErrors
{
    public static readonly Error NotFound = Error.NotFound("Question.NotFound", "Soru bulunamadı.");

    public static readonly Error InUse = Error.BusinessRule(
        "Question.InUse", "Bu soru bir şablonda kullanıldığı için silinemez. Yeni şablonlarda görünmemesi için pasif yapabilirsiniz.");

    public static readonly Error TemplateNotFound = Error.NotFound("InterviewTemplate.NotFound", "Mülakat şablonu bulunamadı.");
}
