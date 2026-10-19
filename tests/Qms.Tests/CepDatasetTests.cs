using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Collection;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
using Qms.Domain.Security;
using Qms.Infrastructure;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class CepDatasetTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly ServiceProvider services;
    private readonly ICepDatasetService service;
    private readonly ClaimsPrincipal technician = User(Roles.Tecnico);
    private int characteristicId,
        instrumentId;
    private readonly DateTimeOffset measured = DateTimeOffset.UtcNow.AddHours(-1);

    public CepDatasetTests()
    {
        var cs = $"Data Source=cep-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        connection = new(cs);
        connection.Open();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Default"] = cs }
            )
            .Build();
        services = new ServiceCollection()
            .AddLogging()
            .AddQmsInfrastructure(config)
            .BuildServiceProvider();
        service = services.GetRequiredService<ICepDatasetService>();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        db.Database.EnsureCreated();
        var a = new Area { Name = "Montagem" };
        db.Add(a);
        db.SaveChanges();
        var p = new Product { AreaId = a.Id, Name = "Produto A" };
        db.Add(p);
        db.SaveChanges();
        var l = new Line { ProductId = p.Id, Name = "Linha 1" };
        db.Add(l);
        db.SaveChanges();
        var s = new Station { LineId = l.Id, Name = "Posto 10" };
        db.Add(s);
        db.SaveChanges();
        var t = new Tooling
        {
            StationId = s.Id,
            Code = "TL-001",
            Name = "Dispositivo",
        };
        db.Add(t);
        db.SaveChanges();
        var c = new Characteristic
        {
            ToolingId = t.Id,
            Name = "Altura",
            Unit = "mm",
            SubgroupSize = 3,
            ChartType = ChartType.XbarR,
            Lsl = 9.5,
            Usl = 10.5,
            Nominal = 10,
        };
        var i = new Instrument
        {
            Code = "INS-001",
            Name = "Paquímetro",
            Type = "Digital",
            CalibrationDue = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
        };
        db.AddRange(c, i);
        db.SaveChanges();
        characteristicId = c.Id;
        instrumentId = i.Id;
    }

    private static ClaimsPrincipal User(string role) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "operator-1"),
                    new Claim(ClaimTypes.Role, role),
                ],
                "test"
            )
        );

    private Task<CepDataset> Create() =>
        service.CreateAsync(characteristicId, "CEP de teste", technician);

    [Fact]
    public async Task Analysis_is_reproducible_immutable_idempotent_and_audited()
    {
        var analyses = services.GetRequiredService<ICepAnalysisService>();
        var engineer = User(Roles.Engenheiro);
        var set = await Create();
        await Assert.ThrowsAsync<ArgumentException>(() => analyses.RunAsync(set.Id, engineer));
        set = await service.SaveAsync(
            new(set.Id, set.Version, set.Title, Rows(), true),
            technician
        );
        var saved = await analyses.RunAsync(set.Id, engineer);
        var repeat = await analyses.RunAsync(set.Id, engineer);
        Assert.Equal(saved.Id, repeat.Id);
        Assert.Single(await analyses.ListAsync(set.Id, User(Roles.Leitor)));
        var input = System.Text.Json.JsonSerializer.Deserialize<CepDataset>(saved.InputJson)!;
        var result = System.Text.Json.JsonSerializer.Deserialize<Qms.Statistics.CepResult>(
            saved.ResultJson
        )!;
        var reproduced = Qms.Statistics.CepCalculator.Calculate(
            input
                .Samples.Select(x => new Qms.Statistics.CepReading(
                    x.Position,
                    x.Value1!.Value,
                    x.Value2!.Value,
                    x.Value3!.Value
                ))
                .ToArray(),
            input.Nominal,
            input.Lsl,
            input.Usl
        );
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(reproduced), saved.ResultJson);
        Assert.Equal(10.03, result.Mean, 10);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        Assert.Single(await db.AuditLogs.Where(x => x.Entity == "cep_analysis").ToListAsync());
        var stored = await db.CepAnalyses.SingleAsync();
        stored.ResultJson = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(Roles.Tecnico)]
    [InlineData(Roles.Leitor)]
    public async Task Analysis_write_permission_is_checked_on_server(string role)
    {
        var analyses = services.GetRequiredService<ICepAnalysisService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            analyses.RunAsync(1, User(role))
        );
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            analyses.ListAsync(null, new ClaimsPrincipal())
        );
        Assert.Empty(await analyses.ListAsync(null, User(Roles.Leitor)));
    }

    private CepRowInput[] Rows(int complete = 25) =>
        Enumerable
            .Range(1, 25)
            .Select(i => new CepRowInput(
                i,
                i <= complete ? "10,02" : "",
                i <= complete ? "10.04" : "",
                i <= complete ? "10,03" : "",
                instrumentId,
                measured,
                "T1",
                "L-1"
            ))
            .ToArray();

    [Fact]
    public async Task Create_snapshots_specification_and_empty_positions_with_audit()
    {
        var set = await Create();
        Assert.Equal(25, set.Samples.Count);
        Assert.Equal(0, set.CompleteSamples);
        Assert.Equal(0, set.Readings);
        Assert.Equal("Montagem / Produto A / Linha 1 / Posto 10 / TL-001", set.ProcessPath);
        Assert.Equal(9.5, set.Lsl);
        Assert.Equal("Altura", set.CharacteristicName);
        Assert.Single(await service.HistoryAsync(set.Id, User(Roles.Leitor)));
    }

    [Fact]
    public async Task Partial_draft_survives_reload_and_computes_only_complete_samples()
    {
        var set = await Create();
        var rows = Rows(2);
        rows[2] = rows[2] with { Value1 = "10" };
        await service.SaveAsync(new(set.Id, set.Version, set.Title, rows, false), technician);
        var loaded = await service.GetAsync(set.Id, User(Roles.Leitor));
        Assert.Equal(2, loaded.CompleteSamples);
        Assert.Equal(7, loaded.Readings);
        Assert.Equal(8, loaded.ProgressPercent);
        Assert.Equal(10.03, loaded.Samples[0].Mean!.Value, 10);
        Assert.Equal(0.02, loaded.Samples[0].Range!.Value, 10);
        Assert.Null(loaded.Samples[2].Mean);
        Assert.Null(loaded.CompletedAt);
        var history = await service.HistoryAsync(set.Id, technician);
        Assert.Equal(2, history.Count);
        Assert.Contains("10.02", history[0].After);
        Assert.Contains("operator-1", history[0].After);
    }

    [Fact]
    public async Task Complete_requires_75_readings_and_freezes_dataset()
    {
        var set = await Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new(set.Id, set.Version, set.Title, Rows(24), true), technician)
        );
        Assert.Equal(1, (await service.GetAsync(set.Id, technician)).Version);
        var saved = await service.SaveAsync(
            new(set.Id, set.Version, set.Title, Rows(), true),
            technician
        );
        Assert.Equal(100, saved.ProgressPercent);
        Assert.Equal(75, saved.Readings);
        Assert.NotNull(saved.CompletedAt);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new(saved.Id, saved.Version, saved.Title, Rows(), false), technician)
        );
        Assert.Equal("cep.complete", (await service.HistoryAsync(set.Id, technician))[0].Action);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    [InlineData("abc")]
    public async Task Invalid_numbers_do_not_change_saved_draft_or_history(string value)
    {
        var set = await Create();
        var rows = Rows();
        rows[15] = rows[15] with { Value2 = value };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new(set.Id, 1, set.Title, rows, false), technician)
        );
        Assert.Equal(0, (await service.GetAsync(set.Id, technician)).Readings);
        Assert.Single(await service.HistoryAsync(set.Id, technician));
    }

    [Fact]
    public async Task Stale_version_cannot_overwrite_more_recent_save()
    {
        var set = await Create();
        await service.SaveAsync(new(set.Id, 1, "Primeiro", Rows(1), false), technician);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new(set.Id, 1, "Sobrescrever", Rows(), false), technician)
        );
        var saved = await service.GetAsync(set.Id, technician);
        Assert.Equal("Primeiro", saved.Title);
        Assert.Equal(1, saved.CompleteSamples);
    }

    [Fact]
    public async Task Reader_can_consult_but_cannot_create_or_save()
    {
        var set = await Create();
        var reader = User(Roles.Leitor);
        Assert.Equal(set.Id, (await service.GetAsync(set.Id, reader)).Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(characteristicId, "Negado", reader)
        );
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SaveAsync(new(set.Id, 1, set.Title, Rows(), false), reader)
        );
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetAsync(set.Id, new ClaimsPrincipal())
        );
    }

    [Theory]
    [InlineData("missing-instrument")]
    [InlineData("expired-instrument")]
    [InlineData("future")]
    [InlineData("invalid-shift")]
    [InlineData("wrong-revision")]
    [InlineData("duplicate-position")]
    public async Task Reject_invalid_traceability_and_structure(string scenario)
    {
        var set = await Create();
        var rows = Rows();
        if (scenario == "missing-instrument")
            rows[0] = rows[0] with { InstrumentId = null };
        if (scenario == "future")
            rows[0] = rows[0] with { CollectedAt = DateTimeOffset.UtcNow.AddDays(1) };
        if (scenario == "invalid-shift")
            rows[0] = rows[0] with { Shift = "T9" };
        if (scenario == "duplicate-position")
            rows[0] = rows[0] with { Position = 2 };
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
            if (scenario == "expired-instrument")
                (await db.Instruments.SingleAsync()).CalibrationDue = DateOnly.FromDateTime(
                    measured.UtcDateTime.AddDays(-1)
                );
            if (scenario == "wrong-revision")
                (await db.Characteristics.SingleAsync()).UpdateSpecification(
                    9,
                    10,
                    11,
                    3,
                    ChartType.XbarR
                );
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new(set.Id, 1, set.Title, rows, false), technician)
        );
        Assert.Equal(0, (await service.GetAsync(set.Id, technician)).Readings);
    }

    private async Task<Subgroup> Source()
    {
        using var scope = services.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<ICollectionService>()
            .SaveAsync(
                new(
                    characteristicId,
                    ["10.02", "10.04", "10.03"],
                    instrumentId,
                    measured,
                    "T1",
                    "L-1",
                    null
                ),
                "original-operator"
            );
    }

    [Fact]
    public async Task Database_source_matches_frontend_means_and_preserves_origin()
    {
        var set = await Create();
        var source = await Source();
        var preview = await service.SourcesAsync(
            set.Id,
            measured.AddMinutes(-1),
            measured.AddMinutes(1),
            "T1",
            "L-1",
            technician
        );
        Assert.Single(preview);
        var rows = Rows(1);
        rows[0] = rows[0] with
        {
            SourceSubgroupId = source.Id,
            CollectedAt = preview[0].Subgroup.CollectedAt,
        };
        var saved = await service.SaveAsync(new(set.Id, 1, set.Title, rows, false), technician);
        Assert.Equal("base", saved.Samples[0].Source);
        Assert.Equal(source.Id, saved.Samples[0].SourceSubgroupId);
        Assert.Equal("original-operator", saved.Samples[0].OperatorId);
        Assert.Equal(10.03, saved.Samples[0].Mean!.Value, 10);
        Assert.Empty(
            await service.SourcesAsync(
                set.Id,
                measured.AddMinutes(-1),
                measured.AddMinutes(1),
                "T2",
                "",
                technician
            )
        );
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("excluded")]
    [InlineData("duplicate")]
    public async Task Base_selection_is_revalidated_on_save(string scenario)
    {
        var set = await Create();
        var source = await Source();
        var rows = Rows(1);
        rows[0] = rows[0] with { SourceSubgroupId = source.Id };
        if (scenario == "tamper")
            rows[0] = rows[0] with { Value1 = "15" };
        if (scenario == "duplicate")
            rows[1] = rows[0] with { Position = 2 };
        if (scenario == "excluded")
        {
            using var scope = services.CreateScope();
            await scope
                .ServiceProvider.GetRequiredService<ICollectionService>()
                .ExcludeAsync(source.Id, "Teste", "engineer");
        }
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new(set.Id, 1, set.Title, rows, false), technician)
        );
        Assert.Equal(0, (await service.GetAsync(set.Id, technician)).Readings);
    }

    [Fact]
    public async Task Daily_uses_sao_paulo_day_boundaries()
    {
        var set = await Create();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
            (await db.CepDatasets.SingleAsync()).CreatedAt = new DateTimeOffset(
                2026,
                10,
                17,
                2,
                59,
                0,
                TimeSpan.Zero
            );
            await db.SaveChangesAsync();
        }
        Assert.Single((await service.DailyAsync(new DateOnly(2026, 10, 16), technician)).Datasets);
        Assert.Empty((await service.DailyAsync(new DateOnly(2026, 10, 17), technician)).Datasets);
    }

    public void Dispose()
    {
        services.Dispose();
        connection.Dispose();
    }
}
