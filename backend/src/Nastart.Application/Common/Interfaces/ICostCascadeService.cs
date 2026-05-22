namespace Nastart.Application.Common.Interfaces;

public interface ICostCascadeService
{
    Task<CascadeResult> RecalculateForIngredientAsync(
        Guid IngredientId,
        CancellationToken cancellationToken
    );
}

public record CascadeResult(int AffectedRecipes, int FailedRecipes);