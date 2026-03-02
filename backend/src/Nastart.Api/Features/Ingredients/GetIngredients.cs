using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

public record GetIngredientsQuery : IRequest<List<Ingredient>>;

public class GetIngredientsHandler(NastartDbContext db) : IRequestHandler<GetIngredientsQuery, List<Ingredient>>
{
    public async Task<List<Ingredient>> Handle(
        GetIngredientsQuery request,
        CancellationToken cancellationToken)
    {
        return await db.Ingredients.ToListAsync(cancellationToken);
    }
}