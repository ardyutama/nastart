using Nastart.Api.Features.Users;

namespace Nastart.Api.Features.Ingredients;

public class Ingredient
{
    public Guid Id { get; set; }
    public Guid? Userid { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public decimal? CurrentPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool HasPrice => CurrentPrice.HasValue;
    public bool IsLowStock => CurrentStock <= MinStock;
    public User? User { get; set; } = null;
    public Brand? Brand { get; set; }
    public Category? Category { get; set; }
}

public class Category
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<Ingredient> Ingredients { get; set; } = [];
}

public class Brand
{
    public Guid Id {get; set;}
    public required string Name {get; set;}
    public ICollection<Ingredient> Ingredients {get; set;} = [];
}