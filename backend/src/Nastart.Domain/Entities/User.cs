namespace Nastart.Domain.Entities;

using Nastart.Domain.Common;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }

    public ICollection<TelegramLink> TelegramLinks { get; set; } = [];
    public ICollection<Ingredient> Ingredients { get; set; } = [];
}