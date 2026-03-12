using Nastart.Api.Features.Purchases;
using Nastart.Api.Features.Users;

namespace Nastart.Api.Features.Shops;

public class Shop
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public String? Name { get; set; }
    public User? User { get; set; }
    public ICollection<Purchase> Purchases { get; set; } = [];
}