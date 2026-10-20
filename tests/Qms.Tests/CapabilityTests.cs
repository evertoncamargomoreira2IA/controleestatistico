using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Collection;
using Qms.Application.Settings;
using Qms.Domain.Collection;
using Qms.Domain.Security;
using Qms.Infrastructure.Collection;
using Qms.Infrastructure.Persistence;
using Qms.Statistics;

namespace Qms.Tests;

public sealed class CapabilityCalculatorTests
{
    private static CepReading[] Series(double shift = 0) => Enumerable.Range(1, 33)
        .Select(i => { var m = 10 + shift + (i % 2 == 0 ? .1 : -.1); return new CepReading(i, m - .02, m, m + .02); }).ToArray();
    [Fact]
    public void Reference_uses_32_adjacent_ranges_and_33_trio_means()
    {
        var r = CapabilityCalculator.Calculate(Series(), 9.5, 10.5);
        Assert.Equal(9.996969696969697, r.Mean, 11);
        Assert.Equal(.2, r.AverageMovingRange, 11);
        Assert.Equal(.1773049645390071, r.SigmaWithinMeans, 11);
        Assert.Equal(.94, r.Cp, 11);
        Assert.Equal(.93430303030303, r.Cpk, 11);
        Assert.Empty(r.StabilitySignals);
        // Wide changes within each trio do not alter its mean or capability.
        var differentRaw = Series().Select(x => new CepReading(x.Position, x.Second - 2, x.Second, x.Second + 2)).ToArray();
        var other = CapabilityCalculator.Calculate(differentRaw, 9.5, 10.5);
        Assert.Equal(r.Cpk, other.Cpk, 10);
        Assert.Equal(66, other.IndividualOutsideSpec);
        Assert.Equal(0, other.MeansOutsideSpec);
    }
    [Fact]
    public void Cpk_preserves_negative_values_and_shift_reduces_centering()
    {
        var r = CapabilityCalculator.Calculate(Series(1), 9.5, 10.5);
        Assert.Equal(.94, r.Cp, 10); Assert.True(r.Cpk < 0); Assert.Equal(33, r.MeansOutsideSpec);
    }
    [Fact]
    public void Zero_variation_is_unavailable_not_infinite()
    {
        Assert.Throws<ArgumentException>(() => CapabilityCalculator.Calculate(Enumerable.Range(1, 33).Select(i => new CepReading(i, 9, 10, 11)).ToArray(), 9, 11));
    }
    [Theory]
    [InlineData(null, 11.0)]
    [InlineData(9.0, null)]
    [InlineData(11.0, 9.0)]
    [InlineData(10.0, 10.0)]
    [InlineData(double.NaN, 11.0)]
    public void Invalid_or_unilateral_specs_rejected(double? lsl, double? usl) => Assert.Throws<ArgumentException>(() => CapabilityCalculator.Calculate(Series(), lsl, usl));
    [Fact]
    public void Wrong_count_duplicates_and_nonfinite_readings_rejected()
    {
        Assert.Throws<ArgumentException>(() => CapabilityCalculator.Calculate(Series().Take(25).ToArray(), 9, 11));
        var a = Series(); a[32] = a[0]; Assert.Throws<ArgumentException>(() => CapabilityCalculator.Calculate(a, 9, 11));
        a = Series(); a[0] = a[0] with { First = double.PositiveInfinity }; Assert.Throws<ArgumentException>(() => CapabilityCalculator.Calculate(a, 9, 11));
    }
    [Fact]
    public void Trend_is_flagged_and_stored_without_claiming_process_approval()
    {
        var a = Enumerable.Range(1, 33).Select(i => new CepReading(i, 10 + i * .001, 10 + i * .001, 10 + i * .001)).ToArray();
        Assert.Contains(CapabilityCalculator.Calculate(a, 9, 11).StabilitySignals, x => x.Contains("tendência"));
    }
}

public sealed partial class CepDatasetTests
{
    private CepRowInput[] CapabilityRows() => Enumerable.Range(1, 33).Select(i =>
    {
        var m = 10 + (i % 2 == 0 ? .1 : -.1);
        return new CepRowInput(i, (m - .02).ToString("R", CultureInfo.InvariantCulture), m.ToString("R", CultureInfo.InvariantCulture), (m + .02).ToString("R", CultureInfo.InvariantCulture), instrumentId, measured.AddSeconds(i), "T1", "SIMULADO-CP");
    }).ToArray();
    [Fact]
    public async Task Capability_lifecycle_preserves_inputs_history_permissions_and_cep_isolation()
    {
        var analysis = services.GetRequiredService<ICapabilityService>();
        var engineer = User(Roles.Engenheiro);
        var set = await service.CreateAsync(characteristicId, "SIMULADO CP", technician, "CAPABILITY");
        Assert.Equal(33, set.Samples.Count); Assert.Equal(0, set.ProgressPercent);
        await Assert.ThrowsAsync<ArgumentException>(() => analysis.RunAsync(set.Id, engineer));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(set.Id, set.Version, set.Title, Rows(), true), technician));
        var partial = CapabilityRows(); partial[32] = partial[32] with { Value3 = "" };
        set = await service.SaveAsync(new(set.Id, set.Version, set.Title, partial, false), technician);
        Assert.Equal(32, set.CompleteSamples); Assert.Equal(96, set.ProgressPercent);
        var oldVersion = set.Version;
        set = await service.SaveAsync(new(set.Id, set.Version, set.Title, CapabilityRows(), true), technician);
        Assert.Equal(99, set.Readings); Assert.Equal(100, set.ProgressPercent);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(set.Id, oldVersion, set.Title, CapabilityRows(), true), technician));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => analysis.RunAsync(set.Id, technician));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => analysis.RunAsync(set.Id, User(Roles.Leitor)));
        var cep = services.GetRequiredService<ICepAnalysisService>();
        await Assert.ThrowsAsync<ArgumentException>(() => cep.RunAsync(set.Id, engineer));
        var saved = await analysis.RunAsync(set.Id, engineer);
        Assert.Equal(saved.Id, (await analysis.RunAsync(set.Id, engineer)).Id);
        var result = JsonSerializer.Deserialize<CapabilityStudyResult>(saved.ResultJson)!;
        Assert.Equal(.94, result.Result.Cp, 10);
        Assert.Equal(CapabilityCalculator.Method, saved.Method);
        Assert.Single(await analysis.ListAsync(User(Roles.Leitor)));
        Assert.Equal(3, (await service.HistoryAsync(set.Id, User(Roles.Leitor))).Count);
        var day = DateOnly.FromDateTime(LocalTime.ToLocal(set.CreatedAt).DateTime);
        Assert.Empty((await service.DailyAsync(day, technician)).Datasets);
        Assert.Single((await service.DailyAsync(day, technician, "CAPABILITY")).Datasets);
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        Assert.Single(await db.AuditLogs.Where(x => x.Entity == "capability_study").ToArrayAsync());
        var stored = await db.CapabilityStudies.SingleAsync(); stored.ResultJson = "alterado";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Capability_rejects_reverse_chronology_and_preserves_draft()
    {
        var set = await service.CreateAsync(characteristicId, "CP", technician, "CAPABILITY");
        var rows = CapabilityRows(); rows[3] = rows[3] with { CollectedAt = measured.AddMinutes(-1) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(set.Id, set.Version, set.Title, rows, true), technician));
        Assert.Equal(0, (await service.GetAsync(set.Id, technician)).Readings);
        var normal = await Create(); await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<ICapabilityService>().RunAsync(normal.Id, User(Roles.Engenheiro)));
    }
    [Fact]
    public async Task Changing_targets_creates_new_study_without_overwriting_old_result()
    {
        var set = await service.CreateAsync(characteristicId, "CP revisão", technician, "CAPABILITY");
        await service.SaveAsync(new(set.Id, set.Version, set.Title, CapabilityRows(), true), technician);
        var analysis = services.GetRequiredService<ICapabilityService>();
        var first = await analysis.RunAsync(set.Id, User(Roles.Qualidade)); var original = first.ResultJson;
        using (var scope = services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISettingsService>().SaveAsync(QualitySettings.Defaults with { Cp = new(1, 1.67) }, "Meta CP 1,67", "operator-1");
            Assert.True(result.Succeeded);
        }
        var second = await analysis.RunAsync(set.Id, User(Roles.Qualidade));
        Assert.NotEqual(first.Id, second.Id); Assert.NotEqual(first.SettingsVersion, second.SettingsVersion);
        Assert.Equal(original, (await analysis.ListAsync(technician)).Single(x => x.Id == first.Id).ResultJson);
        Assert.Equal(1.67, JsonSerializer.Deserialize<CapabilityStudyResult>(second.ResultJson)!.CpTarget);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => analysis.ListAsync(new()));
    }
    [Fact]
    public async Task Capability_dataset_cannot_be_linked_to_a_cep_plan()
    {
        var op = await Operations(); var plan = await op.PlanAsync(PlanInput(), User(Roles.Engenheiro));
        var set = await service.CreateAsync(characteristicId, "CP separado", technician, "CAPABILITY");
        await Assert.ThrowsAsync<ArgumentException>(() => op.LinkAsync(plan.Id, set.Id, plan.Version, technician));
        Assert.Equal(0, (await op.BoardAsync(plan.Day, technician)).Plans.Single().Linked);
    }
}

public sealed class StudyFileReaderTests
{
    [Theory]
    [InlineData("corrompido.xlsx")]
    [InlineData("incompativel.zip")]
    public async Task Corrupted_and_unsupported_files_return_validation_errors(string filename)
    {
        using var data = new MemoryStream(Encoding.UTF8.GetBytes("arquivo sem conteúdo Excel"));
        await Assert.ThrowsAsync<ArgumentException>(() => new StudyFileReader().ReadAsync(filename, data, 33));
    }
    [Fact]
    public async Task Oversized_file_is_rejected_before_decoding()
    {
        using var data = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        await Assert.ThrowsAsync<ArgumentException>(() => new StudyFileReader().ReadAsync("grande.xlsx", data, 33));
    }
    [Fact]
    public async Task Csv_accepts_decimal_comma_zero_negative_and_rejects_excess_without_truncating()
    {
        var reader = new StudyFileReader();
        using var valid = new MemoryStream(Encoding.UTF8.GetBytes("amostra;leitura1;leitura2;leitura3\n1;0;-1,2;3.4"));
        var rows = await reader.ReadAsync("simulado.csv", valid, 25); Assert.Equal("-1,2", rows[0][1]);
        var csv = "amostra;leitura1;leitura2;leitura3\n" + string.Join("\n", Enumerable.Range(1, 33).Select(i => $"{i};1;2;3"));
        using var excess = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync("cp.csv", excess, 25));
    }
    [Theory]
    [InlineData("amostra;leitura1;leitura2;leitura3\n1;1;2;NaN")]
    [InlineData("amostra;leitura1;leitura2;leitura3\n2;1;2;3")]
    [InlineData("amostra;leitura1;leitura2;leitura3\n1;1;;3")]
    [InlineData("amostra;leitura1;leitura2;leitura3\n1;1;2;3;4")]
    [InlineData("titulo\n1;1;2;3")]
    public async Task Malformed_files_rejected(string csv)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await Assert.ThrowsAsync<ArgumentException>(() => new StudyFileReader().ReadAsync("arquivo.csv", input, 33));
    }
    [Fact]
    public async Task Xlsx_accepts_first_tab_and_rejects_formulas_in_raw_readings()
    {
        using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Leituras");
        var headers = new[] { "amostra", "leitura1", "leitura2", "leitura3" }; for (var i = 0; i < 4; i++) sheet.Cell(1, i + 1).Value = headers[i];
        for (var i = 1; i <= 4; i++) sheet.Cell(2, i).Value = i;
        using var data = new MemoryStream(); book.SaveAs(data); data.Position = 0;
        Assert.Single(await new StudyFileReader().ReadAsync("arquivo.xlsx", data, 33));
        sheet.Cell(2, 2).FormulaA1 = "1+1"; using var bad = new MemoryStream(); book.SaveAs(bad); bad.Position = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => new StudyFileReader().ReadAsync("arquivo.xlsx", bad, 33));
    }
    [Theory]
    [InlineData("CEP_25x3", 25)]
    [InlineData("CP_CPK_33x3", 33)]
    public async Task Delivered_csv_and_excel_import_identically_and_match_independent_reference(string name, int count)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qms.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var prefix = Path.Combine(root.FullName, "src", "Qms.Web", "wwwroot", "modelos", "Simulado_" + name);
        var reader = new StudyFileReader();
        using var csv = File.OpenRead(prefix + ".csv"); using var excel = File.OpenRead(prefix + ".xlsx");
        var a = await reader.ReadAsync("simulado.csv", csv, count); var b = await reader.ReadAsync("simulado.xlsx", excel, count);
        Assert.Equal(count, a.Count); Assert.Equal(count, b.Count);
        double P(string x) => double.Parse(x.Replace(',', '.'), CultureInfo.InvariantCulture);
        for (var i = 0; i < count; i++) Assert.Equal(a[i].Select(P), b[i].Select(P));
        var points = a.Select((x, i) => new CepReading(i + 1, P(x[0]), P(x[1]), P(x[2]))).ToArray();
        if (count == 33)
        {
            var result = CapabilityCalculator.Calculate(points, 9.5, 10.5);
            Assert.Equal(3.133333333333333, result.Cp, 10); Assert.Equal(3.12763636363636, result.Cpk, 10);
        }
        else
        {
            var result = CepCalculator.Calculate(points, 10, 9.5, 10.5);
            Assert.Equal(10, result.Mean, 10); Assert.Equal(9.91816, result.XLower, 10); Assert.Equal(10.08184, result.XUpper, 10);
            Assert.Empty(result.Signals);
        }
    }
}
