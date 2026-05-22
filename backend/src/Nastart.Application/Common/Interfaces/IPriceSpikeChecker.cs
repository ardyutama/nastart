namespace Nastart.Application.Common.Interfaces;

public interface IPriceSpikeChecker
{
    Task CheckAndDispatchAsync(
        Guid ingredientId,
        Guid userId,
        decimal newPrice,
        CancellationToken cancellationToken
    );
}