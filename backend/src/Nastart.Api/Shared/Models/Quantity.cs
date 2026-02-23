namespace Nastart.Api.Shared.Models;

public sealed record Quantity(decimal Value, string Unit)
{
    public static Quantity Zero(string unit) => new(0, unit);
    public static Quantity Kilograms(decimal value) => new(value, "kg");
    public static Quantity Grams(decimal value) => new(value, "g");
    public static Quantity Liters(decimal value) => new(value, "L");
    public static Quantity Pieces(decimal value) => new(value, "pcs");
    public bool IsEmpty => Value <= 0;
    public override string ToString() => $"{Value:N2} {Unit}";
}