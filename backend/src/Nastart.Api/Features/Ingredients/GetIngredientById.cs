using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

public record GetIngredientsByIdQuery(Guid Id) : IRequest<Ingredient?>;

public class GetIngredientsByIdHandler(NastartDbContext db) : IRequestHandler<GetIngredientsByIdQuery, Ingredient?>
{
    public async Task<Ingredient?> Handle(
        GetIngredientsByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await db.Ingredients.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}