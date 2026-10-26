namespace Recruitment.Application.Abstractions;

/// <summary>Kullanıcı adı bazında başarısız giriş sayacı ve geçici kilit.</summary>
public interface ILoginThrottle
{
    bool IsLockedOut(string username);

    void RecordFailure(string username);

    void Reset(string username);
}
