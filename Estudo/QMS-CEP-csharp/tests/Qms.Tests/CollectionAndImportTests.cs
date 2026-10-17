using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Domain.MasterData;
using Qms.Infrastructure.Collection;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class CollectionAndImportTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly QmsDbContext db;
    private readonly RecordingAudit audit = new();
    private readonly Characteristic characteristic;
    private readonly Instrument instrument;

    public CollectionAndImportTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();

        var area = new Area { Name = "Montagem" };
        db.Add(area);
        db.SaveChanges();
        var product = new Product { AreaId = area.Id, Name = "Produto A" };
        db.Add(product);
        db.SaveChanges();
        var line = new Line { ProductId = product.Id, Name = "Linha 1" };
        db.Add(line);
        db.SaveChanges();
        var station = new Station { LineId = line.Id, Name = "Posto 10" };
        db.Add(station);
        db.SaveChanges();
        var tooling = new Tooling
        {
            StationId = station.Id,
            Code = "TL-001",
            Name = "TL-001",
        };
        db.Add(tooling);
        db.SaveChanges();
        characteristic = new Characteristic
        {
            ToolingId = tooling.Id,
            Name = "Altura",
            Unit = "mm",
            Lsl = 9.5,
            Nominal = 10,
            Usl = 10.5,
            SubgroupSize = 3,
            ChartType = ChartType.XbarR,
        };
        instrument = new Instrument
        {
            Code = "INS-001",
            Name = "Paquímetro",
            Type = "Paquímetro",
            CalibrationDue = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
        };
        db.AddRange(characteristic, instrument);
        db.SaveChanges();
    }

    [Fact]
    public async Task Save_stores_measurements_in_order_with_revision_and_audit()
    {
        var service = new CollectionService(db, audit);
        var saved = await service.SaveAsync(
            new CollectionInput(
                characteristic.Id,
                ["10,01", "9.99", "10.02"],
                instrument.Id,
                DateTimeOffset.UtcNow.AddHours(-1),
                "T1",
                "L-1",
                null
            ),
            "user-1"
        );

        var stored = await db.Subgroups.Include(x => x.Measurements).SingleAsync();
        Assert.Equal(saved.Id, stored.Id);
        Assert.Equal("online", stored.Source);
        Assert.Equal(characteristic.Revision, stored.CharacteristicRevision);
        Assert.Equal(
            [10.01, 9.99, 10.02],
            stored.Measurements.OrderBy(m => m.Position).Select(m => m.Value)
        );
        Assert.Contains("subgroup.create", audit.Actions);
    }

    [Fact]
    public async Task Exclude_requires_reason_and_marks_subgroup()
    {
        var service = new CollectionService(db, audit);
        var saved = await service.SaveAsync(
            new CollectionInput(
                characteristic.Id,
                ["10", "10", "10"],
                instrument.Id,
                DateTimeOffset.UtcNow.AddHours(-1),
                "T1",
                null,
                null
            ),
            "user-1"
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExcludeAsync(saved.Id, "  ", "user-1")
        );
        Assert.True(await service.ExcludeAsync(saved.Id, "Peça trocada", "user-1"));
        var stored = await db.Subgroups.SingleAsync();
        Assert.True(stored.IsExcluded);
        Assert.Equal("Peça trocada", stored.ExclusionReason);
        Assert.Contains("subgroup.exclude", audit.Actions);
    }

    [Fact]
    public async Task Csv_preview_flags_invalid_row_and_commit_imports_only_valid_rows()
    {
        var service = new ImportService(db, audit);
        var bytes = Csv(
            Row("13/10/2026 08:00", "9,8", "10", "10,2"),
            Row("13/10/2026 10:00", "9.7", "10", "10.1"),
            Row("13/10/2026 12:00", "abc", "10", "10")
        );

        var preview = await service.PreviewAsync(
            await service.ParseAsync("dados.csv", new MemoryStream(bytes))
        );
        Assert.Equal(2, preview.Count(row => row.IsValid));
        var invalid = Assert.Single(preview, row => !row.IsValid);
        Assert.Contains(invalid.Errors, error => error.Contains("posição 1"));
        Assert.Equal(4, invalid.Line);

        var batch = await service.CommitAsync("dados.csv", bytes, "user-1");
        Assert.Equal((3, 2, 1), (batch.RowsTotal, batch.RowsImported, batch.RowsRejected));
        Assert.Equal("completed_with_errors", batch.Status);
        Assert.Equal(2, await db.Subgroups.CountAsync(x => x.Source == "upload"));
        Assert.Contains("import.commit", audit.Actions);

        var again = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CommitAsync("dados.csv", bytes, "user-1")
        );
        Assert.Contains("já foi importado", again.Message);
    }

    [Fact]
    public async Task Rejects_files_over_five_megabytes()
    {
        var service = new ImportService(db, audit);
        var big = new byte[ImportService.MaxFileBytes + 1];
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ParseAsync("grande.csv", new MemoryStream(big))
        );
        Assert.Contains("5 MB", error.Message);
    }

    [Fact]
    public async Task Xlsx_template_filled_in_imports_successfully()
    {
        var template = new TemplateService().BuildXlsx(characteristic);
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheet("Coleta");
        var headers = sheet
            .Row(1)
            .CellsUsed()
            .ToDictionary(cell => cell.GetString(), cell => cell.Address.ColumnNumber);
        Assert.True(headers.ContainsKey("v1") && headers.ContainsKey("v3"));

        // Overwrite the sample row (row 2) with real data.
        var row = sheet.Row(2);
        row.Cell(headers["tooling_code"]).Value = "TL-001";
        row.Cell(headers["characteristic_name"]).Value = "Altura";
        row.Cell(headers["characteristic_id"]).Value = characteristic.Id;
        row.Cell(headers["data_hora"]).Value = new DateTime(2026, 10, 13, 8, 0, 0);
        row.Cell(headers["instrumento_codigo"]).Value = "INS-001";
        row.Cell(headers["turno"]).Value = "T1";
        row.Cell(headers["lote"]).Value = "L-9";
        row.Cell(headers["observacao"]).Value = "";
        row.Cell(headers["v1"]).Value = 10.01;
        row.Cell(headers["v2"]).Value = 9.99;
        row.Cell(headers["v3"]).Value = 10.0;
        using var output = new MemoryStream();
        workbook.SaveAs(output);

        var batch = await new ImportService(db, audit).CommitAsync(
            "coleta.xlsx",
            output.ToArray(),
            "user-1"
        );
        Assert.Equal(1, batch.RowsImported);
        Assert.Equal(0, batch.RowsRejected);
    }

    [Fact]
    public void Csv_template_starts_with_utf8_bom_and_uses_semicolons()
    {
        var bytes = new TemplateService().BuildCsv(characteristic);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var header = Encoding.UTF8.GetString(bytes[3..]).Split('\n')[0];
        Assert.Contains(";", header);
        Assert.Contains("v3", header);
    }

    private string Row(string when, params string[] values) =>
        string.Join(
            ';',
            new[]
            {
                "TL-001",
                "Altura",
                characteristic.Id.ToString(),
                when,
                "INS-001",
                "T1",
                "L-1",
                "",
            }.Concat(values)
        );

    private static byte[] Csv(params string[] rows) =>
        Encoding.UTF8.GetBytes(
            string.Join(
                "\n",
                new[]
                {
                    "tooling_code;characteristic_name;characteristic_id;data_hora;instrumento_codigo;turno;lote;observacao;v1;v2;v3",
                }.Concat(rows)
            )
        );

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private sealed class RecordingAudit : IAuditService
    {
        public List<string> Actions { get; } = [];

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
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }
}
