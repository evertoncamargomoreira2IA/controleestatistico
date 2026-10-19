using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Qms.Domain.Collection;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class CepMigrationTests
{
    [Fact]
    public async Task Upgrade_from_previous_collection_schema_preserves_existing_readings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection().AddLogging();
        // Other domain tests use Identity's default schema. Isolate this provider's model
        // cache because migrations intentionally use the application's Identity v3 schema.
        services.AddDbContext<QmsDbContext>(options =>
            options.UseSqlite(connection).EnableServiceProviderCaching(false)
        );
        services
            .AddIdentityCore<ApplicationUser>(options =>
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3
            )
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<QmsDbContext>();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261016123136_F2Collection");
        var previous = new Subgroup
        {
            CharacteristicId = 1,
            CharacteristicRevision = 1,
            OperatorId = "legacy-operator",
            CollectedAt = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero),
            Source = "online",
            Measurements =
            [
                new() { Position = 1, Value = 10.02 },
                new() { Position = 2, Value = 10.04 },
                new() { Position = 3, Value = 10.03 },
            ],
        };
        db.Subgroups.Add(previous);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var preserved = await db.Subgroups.Include(x => x.Measurements).SingleAsync();
        Assert.Equal("legacy-operator", preserved.OperatorId);
        Assert.Equal(
            new[] { 10.02, 10.04, 10.03 },
            preserved.Measurements.OrderBy(x => x.Position).Select(x => x.Value)
        );
        Assert.Empty(await db.CepDatasets.ToListAsync());
        Assert.Empty(await db.CepAnalyses.ToListAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
