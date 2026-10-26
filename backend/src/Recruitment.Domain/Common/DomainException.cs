namespace Recruitment.Domain.Common;

/// <summary>İhlal edilen iş kuralı. Mesaj kullanıcıya gösterilir, bu yüzden Türkçe yazılır.</summary>
public sealed class DomainException(string message) : Exception(message);
