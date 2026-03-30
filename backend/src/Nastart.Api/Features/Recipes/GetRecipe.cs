using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Recipes;

public record GetRecipesQuery(Guid UserId) : IRequest<List<Recipe>>;

public class GetRecipesHandler(NastartDbContext db) : IRequestHandler<GetRecipesQuery, List<Recipe>>
{
    public async Task<List<Recipe>> Handle(
        GetRecipesQuery request,
        CancellationToken cancellationToken)
    {
        return await db.Recipes
            .Where(r => r.UserId == request.UserId && r.Status == RecipeStatus.Active)
            .Include(r => r.RecipeIngredients)
                .ThenInclude(r => r.Ingredient)
            .ToListAsync();
    }
}