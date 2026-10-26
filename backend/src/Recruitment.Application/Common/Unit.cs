namespace Recruitment.Application.Common;

/// <summary>Değer döndürmeyen command'ların yanıt tipi.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
