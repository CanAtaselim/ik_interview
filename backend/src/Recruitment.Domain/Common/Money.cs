namespace Recruitment.Domain.Common;

/// <summary>Para tutarı: tutar, para birimi ve net mi brüt mü olduğu.</summary>
public sealed record Money
{
    public const string DefaultCurrency = "TRY";

    private Money() { }

    public Money(decimal amount, string? currency = null, bool isNet = true)
    {
        if (amount < 0)
            throw new DomainException("Tutar negatif olamaz.");

        var code = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
        if (code.Length != 3)
            throw new DomainException("Para birimi üç harfli kod olmalı (ör. TRY).");

        Amount = amount;
        Currency = code;
        IsNet = isNet;
    }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = DefaultCurrency;
    public bool IsNet { get; private set; }
}
