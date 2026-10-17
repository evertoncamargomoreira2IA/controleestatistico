using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.Settings;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly QmsDbContext db;
    private readonly Audit audit = new();
    private readonly SettingsService service;

    public SettingsServiceTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        service = new(db, audit);
    }

    [Fact]
    public async Task Save_creates_version_with_author_and_audit()
    {
        Assert.True(
            (await service.SaveAsync(QualitySettings.Defaults, "Inicial", "actor")).Succeeded
        );
        var value = QualitySettings.Defaults with { Cp = new(1.1, 1.4) };
        var result = await service.SaveAsync(value, "Ajuste de Cp", "actor");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Version);
        Assert.Equal(
            "actor",
            db.SettingsVersions.OrderByDescending(x => x.Version).First().CreatedBy
        );
        Assert.Contains("settings.update", audit.Entries);
    }

    [Fact]
    public async Task Rejects_missing_comment_and_bad_threshold_order()
    {
        var missing = await service.SaveAsync(QualitySettings.Defaults, " ", null);
        Assert.False(missing.Succeeded);
        var invalid = await service.SaveAsync(
            QualitySettings.Defaults with
            {
                Cp = new(1.4, 1.3),
            },
            "Alteração",
            null
        );
        Assert.False(invalid.Succeeded);
    }

    [Fact]
    public async Task Diff_lists_only_changed_values()
    {
        await service.SaveAsync(QualitySettings.Defaults, "Inicial", null);
        await service.SaveAsync(
            QualitySettings.Defaults with
            {
                Cp = new(1.1, 1.4),
            },
            "Ajuste",
            null
        );
        var history = await service.GetHistoryAsync();
        Assert.Single(history[0].Diff);
        Assert.Contains("Cp", history[0].Diff.Keys);
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private sealed class Audit : IAuditService
    {
        public List<string> Entries = [];

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
            Entries.Add(action);
            return Task.CompletedTask;
        }
    }
}
