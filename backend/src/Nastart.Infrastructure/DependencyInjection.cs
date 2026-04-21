using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Application.Common.Interfaces;
using Nastart.Infrastructure.Persistence;

namespace Nastart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
            ).UseSnakeCaseNamingConvention()
        );

        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());

        return services;
    }
}