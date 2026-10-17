using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Domain.MasterData;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class MasterDataServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly QmsDbContext db;
    private readonly Audit audit = new();
    private readonly MasterDataService service;

    public MasterDataServiceTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        service = new(db, audit);
    }

    [Fact]
    public async Task Creates_complete_hierarchy_and_audits()
    {
        var area = (await service.CreateAsync(new Area { Name = "A" }, "actor")).Value!;
        var product = (
            await service.CreateAsync(new Product { AreaId = area.Id, Name = "P" }, "actor")
        ).Value!;
        var line = (
            await service.CreateAsync(new Line { ProductId = product.Id, Name = "L" }, "actor")
        ).Value!;
        var station = (
            await service.CreateAsync(new Station { LineId = line.Id, Name = "S" }, "actor")
        ).Value!;
        var tooling = (
            await service.CreateAsync(
                new Tooling
                {
                    StationId = station.Id,
                    Code = "T1",
                    Name = "Tool",
                },
                "actor"
            )
        ).Value!;
        var feature = await service.CreateAsync(
            new Characteristic { ToolingId = tooling.Id, Name = "CTQ" },
            "actor"
        );
        Assert.True(feature.Succeeded, feature.Error);
        Assert.Equal(6, audit.Entries.Count);
    }

    [Fact]
    public async Task Cannot_deactivate_area_with_active_product()
    {
        var area = (await service.CreateAsync(new Area { Name = "A" }, null)).Value!;
        await service.CreateAsync(new Product { AreaId = area.Id, Name = "P" }, null);
        area.IsActive = false;
        var result = await service.UpdateAsync(area, null);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Tooling_code_is_unique_across_stations()
    {
        var area = (await service.CreateAsync(new Area { Name = "A" }, null)).Value!;
        var product = (
            await service.CreateAsync(new Product { AreaId = area.Id, Name = "P" }, null)
        ).Value!;
        var line = (
            await service.CreateAsync(new Line { ProductId = product.Id, Name = "L" }, null)
        ).Value!;
        var s1 = (
            await service.CreateAsync(new Station { LineId = line.Id, Name = "S1" }, null)
        ).Value!;
        var s2 = (
            await service.CreateAsync(new Station { LineId = line.Id, Name = "S2" }, null)
        ).Value!;
        Assert.True(
            (
                await service.CreateAsync(
                    new Tooling
                    {
                        StationId = s1.Id,
                        Code = "DUP",
                        Name = "A",
                    },
                    null
                )
            ).Succeeded
        );
        var result = await service.CreateAsync(
            new Tooling
            {
                StationId = s2.Id,
                Code = "DUP",
                Name = "B",
            },
            null
        );
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Specification_changes_increment_revision_but_name_does_not()
    {
        var a = (await service.CreateAsync(new Area { Name = "A" }, null)).Value!;
        var p = (await service.CreateAsync(new Product { AreaId = a.Id, Name = "P" }, null)).Value!;
        var l = (await service.CreateAsync(new Line { ProductId = p.Id, Name = "L" }, null)).Value!;
        var s = (await service.CreateAsync(new Station { LineId = l.Id, Name = "S" }, null)).Value!;
        var t = (
            await service.CreateAsync(
                new Tooling
                {
                    StationId = s.Id,
                    Code = "T",
                    Name = "T",
                },
                null
            )
        ).Value!;
        var c = (
            await service.CreateAsync(new Characteristic { ToolingId = t.Id, Name = "C" }, null)
        ).Value!;
        c.UpdateSpecification(null, null, 10, 1, ChartType.IMr);
        Assert.True((await service.UpdateAsync(c, null)).Succeeded);
        Assert.Equal(2, c.Revision);
        c.Name = "C2";
        Assert.True((await service.UpdateAsync(c, null)).Succeeded);
        Assert.Equal(2, c.Revision);
        Assert.Contains(audit.Entries, x => x.Action == "characteristics.update");
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private sealed class Audit : IAuditService
    {
        public List<(string Action, object? Before, object? After)> Entries = [];

        public Task RecordAsync(
            string? actorId,
            string entity,
            string? entityId,
            string action,
            object? before = null,
            object? after = null,
            string? ip = null,
            string? origin = null,
            CancellationToken cancellationToken = default
        )
        {
            Entries.Add((action, before, after));
            return Task.CompletedTask;
        }
    }
}
