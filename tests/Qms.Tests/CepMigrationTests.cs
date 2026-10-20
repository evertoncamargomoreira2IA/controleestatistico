using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
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
        await db.GetService<IMigrator>().MigrateAsync("20261020121937_Sprint3CepOperations");
        var area = new Area { Name = "Legado" }; db.Add(area); await db.SaveChangesAsync();
        var product = new Product { AreaId = area.Id, Name = "Legado" }; db.Add(product); await db.SaveChangesAsync();
        var line = new Line { ProductId = product.Id, Name = "Legado" }; db.Add(line); await db.SaveChangesAsync();
        var station = new Station { LineId = line.Id, Name = "Legado" }; db.Add(station); await db.SaveChangesAsync();
        var tooling = new Tooling { StationId = station.Id, Code = "LEGADO", Name = "Legado" }; db.Add(tooling); await db.SaveChangesAsync();
        var characteristic = new Characteristic { ToolingId = tooling.Id, Name = "Altura" }; db.Add(characteristic); await db.SaveChangesAsync();
        // Insert with the actual Sprint 3 schema, before StudyKind exists.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO cep_datasets (CharacteristicId,CharacteristicRevision,CharacteristicName,ProcessPath,Title,CreatedBy,CreatedAt,UpdatedAt,Version) VALUES ({characteristic.Id},1,'Altura','Legado','CEP legado','legacy',0,0,1)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO cep_samples (CepDatasetId,Position,Value1,Value2,Value3,Shift,Lot,OperatorId,Source) VALUES (1,1,9.99,10,10.01,'T1','LEGADO','legacy','frontend')");
        var originalInput = "{\"original\":true}";
        var originalResult = "{\"saved\":true}";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO cep_analyses (DatasetId,DatasetVersion,Method,CreatedAt,CreatedBy,InputJson,ResultJson) VALUES (1,1,'legacy',0,'legacy',{originalInput},{originalResult})");
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var preserved = await db.Subgroups.Include(x => x.Measurements).SingleAsync();
        Assert.Equal("legacy-operator", preserved.OperatorId);
        Assert.Equal(
            new[] { 10.02, 10.04, 10.03 },
            preserved.Measurements.OrderBy(x => x.Position).Select(x => x.Value)
        );
        var legacy = await db.CepDatasets.Include(x => x.Samples).SingleAsync();
        Assert.Equal("CEP", legacy.StudyKind);
        Assert.Equal(25, legacy.RequiredSamples);
        Assert.Equal(new double?[] { 9.99, 10, 10.01 }, new[] { legacy.Samples[0].Value1, legacy.Samples[0].Value2, legacy.Samples[0].Value3 });
        Assert.Equal("{\"saved\":true}", (await db.CepAnalyses.SingleAsync()).ResultJson);
        Assert.Empty(await db.CapabilityStudies.ToListAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
