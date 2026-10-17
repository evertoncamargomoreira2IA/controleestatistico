using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Infrastructure.Auditing;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class AuditServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_audit_cannot_be_changed_or_deleted(bool delete)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options;
        await using var db = new QmsDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await new AuditService(db).RecordAsync("actor-1", "study", "42", "study.create");
        var entry = await db.AuditLogs.SingleAsync();
        if (delete)
            db.AuditLogs.Remove(entry);
        else
            db.Entry(entry).Property(log => log.Action).CurrentValue = "study.changed";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var persisted = await db.AuditLogs.SingleAsync();
        Assert.Equal("study.create", persisted.Action);
    }

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
