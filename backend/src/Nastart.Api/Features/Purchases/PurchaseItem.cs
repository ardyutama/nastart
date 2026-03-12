using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Features.Purchases;

public enum ItemStatus
{
    Pending,
    Confirmed,
    Unmatched
}

public class PurchaseItem
{
    public Guid Id { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid? IngredientId { get; set; }
    public String? RawText { get; set; }
    public Decimal Quantity { get; set; }
    public Decimal Price { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Pending;
    public Ingredient? Ingredient { get; set; }
    public Purchase? Purchase { get; set; } = null!;
}