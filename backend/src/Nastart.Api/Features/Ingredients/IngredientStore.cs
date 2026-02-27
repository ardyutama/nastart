namespace Nastart.Api.Features.Ingredients;

public static class IngredientStore
{
    private static readonly List<Ingredient> _ingredients =
    [
        new() {Id = Guid.NewGuid(), Name = "Flour", Unit = "Kg", CurrentPrice = 12_000m, CurrentStock = 3, MinStock = 1},
        new() {Id = Guid.NewGuid(), Name = "Sugar", Unit = "Kg", CurrentPrice = 9_000m, CurrentStock = 4, MinStock = 1},
        new() {Id = Guid.NewGuid(), Name = "Butter", Unit = "Kg", CurrentPrice = 6_000m, CurrentStock = 5, MinStock = 1},
        new() {Id = Guid.NewGuid(), Name = "Eggs", Unit = "pcs", CurrentPrice = 2_000m, CurrentStock = 5, MinStock = 1},
    ];

    public static List<Ingredient> GetAll() => [.._ingredients];
    public static Ingredient? GetById(Guid id) => _ingredients.FirstOrDefault(x => x.Id == id);
    public static Ingredient Add(Ingredient ingredient)
    {
        ingredient.Id = Guid.NewGuid();
        ingredient.CurrentPrice = ingredient.CurrentPrice;
        ingredient.CurrentStock = ingredient.CurrentStock;
        ingredient.MinStock = ingredient.MinStock;
        ingredient.CreatedAt = DateTimeOffset.UtcNow;
        _ingredients.Add(ingredient);
        return ingredient;
    }
    public static bool Update(Guid id, Ingredient updated)
    {
        var existing = _ingredients.FirstOrDefault(x => x.Id == id);
        if (existing is null) return false;
        existing.Name = updated.Name;
        existing.Unit = updated.Unit;
        existing.CurrentPrice = updated.CurrentPrice;
        return true;
    }

    public static bool Delete(Guid id)
    {
        var existing = _ingredients.FirstOrDefault(x => x.Id == id);
        if (existing is null) return false;
        _ingredients.Remove(existing);
        return true;
    }
}