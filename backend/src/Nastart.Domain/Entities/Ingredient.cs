using Nastart.Domain.Common;

namespace Nastart.Domain.Entities;

public class Ingredient : BaseEntity
{
    public string Name {get; set;} = string.Empty;
    public Guid UserId {get; set;}
    public User User {get; set;} = null!;

    public Guid CategoryId {get; set;}
    public Category? Category {get; set;}

    public Guid UnitId {get; set;}
    public Unit Unit {get; set;} = null!;

    public decimal UnitSize {get; set;}

    public decimal? PriceSpikeThresholdPct {get; set;}

    public ICollection<IngredientPriceHistory> PriceHistory {get; set;} = new List<IngredientPriceHistory>();

}