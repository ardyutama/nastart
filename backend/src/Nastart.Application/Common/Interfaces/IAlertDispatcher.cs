namespace Nastart.Application.Common.Interfaces;

public interface IAlertDispatcher
{
    Task SendPriceSpikeAlertAsync(
        Guid userId,
        Guid ingredientId,
        decimal oldPrice,
        decimal newPrice,
        CancellationToken cancellationToken
    );
}