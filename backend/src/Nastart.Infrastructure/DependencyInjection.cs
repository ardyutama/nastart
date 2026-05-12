using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Application.Common.Interfaces;
using Nastart.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Nastart.Infrastructure.Services;

namespace Nastart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing. " + "Add it to appsettings.Development.json (gitignored - never commit)");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString,
                o => o.SetPostgresVersion(18, 3)
            ).UseSnakeCaseNamingConvention()
        );

        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        if (environment.IsDevelopment())
            services.AddScoped<IEmailService, ConsoleEmailService>();
        else
            throw new InvalidOperationException(
                "No production email provider is configured. " +
                "Register a real IEmailService implementation for non-development environments.");

        return services;
    }
}