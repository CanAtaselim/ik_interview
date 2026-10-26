namespace Recruitment.Domain.Pipeline;

/// <summary>Aşamanın akıştaki anlamı. Geçiş kurallarını bu belirler; adı ve sırası şablonda tutulur.</summary>
public enum StageKind
{
    Applied,
    HrScreening,
    CvReview,
    TechnicalInterview,
    CaseStudy,
    FinalInterview,
    ReferenceCheck,
    Offer,
    Hired,
}

public enum ApplicationStatus
{
    Active,
    Hired,
    Rejected,
    Withdrawn,
}
