using Nastart.Domain.Common;
using Nastart.Domain.Enums;

namespace Nastart.Domain.Entities;

public class IngredientPriceHistory : BaseEntity
{
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal UnitSize { get; set; }

    public PriceSource Source { get; set; }

    public DateTimeOffset CommitedAt { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public Guid? InvoiceLineItemId { get; set; }
}