namespace Nastart.Api.Shared.Models;

public sealed record Money(decimal Amount, string Currency = "IDR")
{
    public static Money Zero => new(0);
    public static Money FromIDR(decimal amount) => new(amount, "IDR");

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} to {other.Currency}");
        return this with { Amount = Amount + other.Amount };
    }

    public Money Substract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot substract {Currency} to {other.Currency}");
        return this with { Amount = Amount - other.Amount };
    }

    public Money Multiply(decimal factor) => this with { Amount = Amount * factor };

    public bool IsZero => Amount == 0;
    public bool IsNegative => Amount < 0;

    public override string ToString() => $"{Currency} {Amount:N0}";
}



public sealed record Percentage(decimal Value)
{
    public static Percentage Zero => new(0);
    public static Percentage FromDecimal(decimal decimalValue) => new(decimalValue * 100);
    public bool IsBelow(decimal threshold) => Value < threshold;
    public bool IsAbove(decimal threshold) => Value > threshold;
    public decimal ToDecimal() => Value / 100;
    public override string ToString() => $"{Value:N1}%";
}