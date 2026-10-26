namespace Recruitment.Application.Common;

/// <summary>
/// Bir işlemin sonucu: ya değer ya da beklenen bir hata. Handler'lar değeri veya
/// <see cref="Error"/>'ı doğrudan döndürebilir (örtük dönüşüm).
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error) => _error = error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Başarısız sonucun değeri okunamaz.");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Başarılı sonucun hatası okunamaz.");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}

public static class Result
{
    /// <summary>Arayüz tipindeki değerler için (C# arayüzlerden örtük dönüşüme izin vermez).</summary>
    public static Result<T> From<T>(T value) => value;
}
