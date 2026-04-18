using Nastart.Domain.Common;

namespace Nastart.Domain.Entities;

public class Category : BaseEntity
{
    public string Name {get; set;} = string.Empty;
    public Guid UserId {get; set;}
    public User User {get; set; } = null!;
    public ICollection<Ingredient> Ingredients {get; set;} = new List<Ingredient>();
}