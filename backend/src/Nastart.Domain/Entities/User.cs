namespace Nastart.Domain.Entities;

using Nastart.Domain.Common;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }

    public ICollection<TelegramLink> TelegramLinks { get; set; } = new List<TelegramLink>();
    // public ICollection<Ingredient> Ingredients {get; set;} = new List<Ingredient>();
}