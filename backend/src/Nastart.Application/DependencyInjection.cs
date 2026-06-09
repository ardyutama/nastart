using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Application.Common.Behaviors;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Services;

namespace Nastart.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<ICostCascadeService, CostCascadeService>();
        services.AddScoped<IPriceSpikeChecker, PriceSpikeChecker>();

        return services;
    }
}