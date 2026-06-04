using Nastart.Domain.Common;

namespace Nastart.Domain.Entities;

public sealed class Recipe : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int PortionCount { get; set; } = 1;
    public decimal CostPerPortion { get; set; }
    public decimal PackagingCost { get; set; } = 0m;
    public decimal TargetMargin { get; set; } = 0m;
    public Guid VersionGroupId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string VersionLabel { get; set; } = "Standart";
    public ICollection<RecipeItem> RecipeItems { get; set; } = [];
}