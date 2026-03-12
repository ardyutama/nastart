using Nastart.Api.Features.Shops;
using Nastart.Api.Features.Users;

namespace Nastart.Api.Features.Purchases;

public enum PurchaseStatus
{
    Received,
    Processing,
    Confirmed,
    Saved
}

public class Purchase
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ShopId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public Decimal TotalAmount { get; set; }
    public String? ReceiptImageUrl { get; set; }
    public String? Notes { get; set; }
    public PurchaseStatus Status { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = [];
    public User? User { get; set; }
    public Shop? Shop { get; set; }
}