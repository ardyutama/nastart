using Nastart.Domain.Common;

namespace Nastart.Domain.Entities;

public sealed class CascadeErrorLog : BaseEntity
{
    public Guid IngredientId { get; set; }
    public Guid RecipeId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}