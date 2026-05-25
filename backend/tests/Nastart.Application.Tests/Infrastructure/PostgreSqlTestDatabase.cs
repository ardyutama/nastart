using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nastart.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Nastart.Application.Tests.Infrastructure;

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    
    public Task InitializeAsync() => _container.StartAsync();

    public async Task<AppDbContext> CreateDbContextAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";

        await using (var adminConnection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await adminConnection.OpenAsync();

            await using var createDatabase = adminConnection.CreateCommand();
            createDatabase.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            await createDatabase.ExecuteNonQueryAsync();
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionStringBuilder.ConnectionString,
                npsql => npsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;
        
        var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}