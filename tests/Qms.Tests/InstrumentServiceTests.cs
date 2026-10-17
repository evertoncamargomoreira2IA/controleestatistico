using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Domain.MasterData;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class InstrumentServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly QmsDbContext db;
    private readonly MasterDataService service;

    public InstrumentServiceTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        service = new(db, new Audit());
    }

    [Fact]
    public async Task Creates_instrument()
    {
        var result = await service.SaveAsync(
            new Instrument
            {
                Code = "I1",
                Name = "Micrômetro",
                Type = "Dimensional",
            },
            "actor"
        );
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, (await service.ListAsync()).Count);
    }

    [Fact]
    public async Task Rejects_duplicate_instrument_code()
    {
        await service.SaveAsync(
            new Instrument
            {
                Code = "I1",
                Name = "A",
                Type = "Dimensional",
            },
            null
        );
        var result = await service.SaveAsync(
            new Instrument
            {
                Code = "I1",
                Name = "B",
                Type = "Dimensional",
            },
            null
        );
        Assert.False(result.Succeeded);
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private sealed class Audit : IAuditService
    {
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
        ) => Task.CompletedTask;
    }
}
