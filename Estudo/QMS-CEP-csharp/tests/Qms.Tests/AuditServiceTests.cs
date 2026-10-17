using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Infrastructure.Auditing;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class AuditServiceTests
{
    [Fact]
    public async Task RecordAsync_persists_audit_and_serializes_dates_as_iso_8601()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options;
        await using var db = new QmsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new AuditService(db);
        var date = new DateTimeOffset(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);
        await service.RecordAsync(
            "actor-1",
            "study",
            "42",
            "study.update",
            after: new { measuredAt = date }
        );

        var entry = await db.AuditLogs.SingleAsync();
        Assert.Equal("actor-1", entry.ActorId);
        Assert.Equal("study.update", entry.Action);
        Assert.Contains("2026-10-16T12:30:00+00:00", entry.AfterJson);
        Assert.Equal(DateTimeOffset.UtcNow.Offset, entry.OccurredAt.Offset);
    }
}
