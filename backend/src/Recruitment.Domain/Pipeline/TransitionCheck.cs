using Recruitment.Domain.Common;

namespace Recruitment.Domain.Pipeline;

/// <summary>Bir aşama geçişine izin var mı; yoksa kullanıcıya gösterilecek neden.</summary>
public readonly record struct TransitionCheck(bool IsAllowed, string? Reason)
{
    public static readonly TransitionCheck Allowed = new(true, null);

    public static TransitionCheck Denied(string reason) => new(false, reason);

    public void ThrowIfDenied()
    {
        if (!IsAllowed)
            throw new DomainException(Reason!);
    }
}
