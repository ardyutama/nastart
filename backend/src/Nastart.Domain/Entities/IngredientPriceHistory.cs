using Nastart.Domain.Common;
using Nastart.Domain.Enums;

namespace Nastart.Domain.Entities;

public class IngredientPriceHistory : BaseEntity
{
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Price { get; init; }
    public decimal UnitSize { get; init; }

    public PriceSource Source { get; init; }

    public DateTimeOffset CommitedAt { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public Guid? InvoiceLineItemId { get; set; }
}