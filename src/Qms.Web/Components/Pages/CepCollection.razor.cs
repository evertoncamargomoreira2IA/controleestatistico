using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Qms.Application.Collection;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
using Qms.Domain.Security;

namespace Qms.Web.Components.Pages;

public partial class CepCollection
{
    [Inject]
    private ICepDatasetService Service { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Auth { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery]
    public int? Id { get; set; }
    private bool IsCapability => new Uri(Navigation.Uri).AbsolutePath == "/cp-coleta";
    private string StudyKind => IsCapability ? "CAPABILITY" : "CEP";
    private string StudyLabel => IsCapability ? "Cp/Cpk" : "CEP";
    private int RequiredSamples => IsCapability ? 33 : 25;
    private string CollectionPath => IsCapability ? "/cp-coleta" : "/cep-coleta";
    private string DailyPath => IsCapability ? "/cp-dia" : "/cep-dia";
    [Inject] private IStudyFileReader FileReader { get; set; } = default!;
    private ClaimsPrincipal user = new();
    private IReadOnlyList<CepChoice> choices = [];
    private IReadOnlyList<Instrument> instruments = [];
    private IReadOnlyList<CepHistoryItem> history = [];
    private IReadOnlyList<CepSourceRow> sources = [];
    private readonly HashSet<int> sourceSelection = [];
    private CepDataset? dataset;
    private List<EntryRow> rows = [];
    private int characteristicId;
    private string title = "";
    private int? instrumentId;
    private DateTime collectedAt = LocalTime.ToLocal(DateTimeOffset.UtcNow).DateTime;
    private DateTime sourceFrom = LocalTime.ToLocal(DateTimeOffset.UtcNow).Date.AddDays(-7);
    private DateTime sourceTo = LocalTime.ToLocal(DateTimeOffset.UtcNow).Date;
    private string shift = "T1",
        lot = "",
        sourceShift = "",
        sourceLot = "",
        pasteText = "";
    private string? error,
        message;
    private bool canWrite,
        busy,
        loading = true,
        confirmReplace,
        confirmComplete,
        dirty,
        searched;
    private bool Editable => canWrite && dataset?.CompletedAt is null;
    private int CompleteCount => rows.Count(x => x.Complete);
    private int ReadingCount =>
        rows.Sum(x => x.Values.Count(v => !string.IsNullOrWhiteSpace(v) && Parse(v).HasValue));

    protected override async Task OnParametersSetAsync()
    {
        await Execute(async () =>
        {
            dataset = null;
            rows = [];
            history = [];
            sources = [];
            sourceSelection.Clear();
            user = (await Auth.GetAuthenticationStateAsync()).User;
            canWrite = Roles.All.Any(r =>
                user.IsInRole(r) && Permissions.Allows(r, Permissions.CollectionCreate)
            );
            choices = await Service.ChoicesAsync(user);
            instruments = await Service.InstrumentsAsync(user);
            if (Id.HasValue)
            {
                var loaded = await Service.GetAsync(Id.Value, user);
                if (loaded.StudyKind != StudyKind) throw new ArgumentException("Este conjunto pertence a outro módulo. Abra pelo progresso correspondente.");
                Load(loaded);
                history = await Service.HistoryAsync(Id.Value, user);
            }
            else
            {
                dataset = null;
                title = StudyLabel + " " + LocalTime.ToLocal(DateTimeOffset.UtcNow).ToString("dd/MM/yyyy");
            }
        });
        loading = false;
    }

    private async Task Execute(Func<Task> work)
    {
        if (busy)
            return;
        busy = true;
        error = message = null;
        try
        {
            await work();
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            error = ex.Message;
        }
        finally
        {
            busy = false;
        }
    }

    private Task CreateAsync() =>
        Execute(async () =>
        {
            var created = await Service.CreateAsync(characteristicId, title, user, StudyKind);
            Navigation.NavigateTo($"{CollectionPath}?id={created.Id}", forceLoad: true);
        });

    private void Load(CepDataset item)
    {
        dataset = item;
        title = item.Title;
        rows = item
            .Samples.Select(s => new EntryRow
            {
                Position = s.Position,
                Values = [Raw(s.Value1), Raw(s.Value2), Raw(s.Value3)],
                InstrumentId = s.InstrumentId,
                CollectedAt = s.CollectedAt.HasValue
                    ? LocalTime.ToLocal(s.CollectedAt.Value).DateTime
                    : collectedAt,
                Shift = string.IsNullOrEmpty(s.Shift) ? shift : s.Shift,
                Lot = s.Lot,
                SourceId = s.SourceSubgroupId,
                Operator = s.OperatorId,
            })
            .ToList();
        dirty = confirmComplete = false;
        var first = rows.FirstOrDefault(r =>
            !r.SourceId.HasValue && r.Values.Any(v => !string.IsNullOrWhiteSpace(v))
        );
        if (first is not null)
        {
            instrumentId = first.InstrumentId;
            collectedAt = first.CollectedAt;
            shift = first.Shift;
            lot = first.Lot;
        }
    }

    private Task SaveAsync(bool complete) =>
        Execute(async () =>
        {
            if (dataset is null || !Editable)
                return;
            if (complete && !confirmComplete)
                throw new ArgumentException("Confirme a conferência antes de concluir.");
            var input = rows.Select(r => new CepRowInput(
                    r.Position,
                    r.Values[0],
                    r.Values[1],
                    r.Values[2],
                    r.InstrumentId,
                    LocalTime.ParseLocal(
                        r.CollectedAt.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)
                    ),
                    r.Shift,
                    r.Lot,
                    r.SourceId
                ))
                .ToArray();
            // Preserve the original subsecond timestamp for linked database rows.
            input = input
                .Select(r =>
                    r.SourceSubgroupId.HasValue
                        ? r with
                        {
                            CollectedAt =
                                rows.Single(x => x.Position == r.Position).OriginalTimestamp
                                ?? dataset
                                    .Samples.Single(x => x.Position == r.Position)
                                    .CollectedAt,
                        }
                        : r
                )
                .ToArray();
            Load(
                await Service.SaveAsync(
                    new(dataset.Id, dataset.Version, title, input, complete),
                    user
                )
            );
            history = await Service.HistoryAsync(dataset.Id, user);
            message = complete
                ? "Conjunto concluído e preservado para consulta. Abra a análise no botão acima."
                : "Rascunho salvo. Leituras e histórico preservados.";
        });

    private void MarkDirty() => dirty = true;

    private void ApplyMetadata()
    {
        foreach (var row in rows.Where(r => !r.SourceId.HasValue))
        {
            row.InstrumentId = instrumentId;
            row.CollectedAt = collectedAt;
            row.Shift = shift;
            row.Lot = lot;
        }
        MarkDirty();
        message = "Identificação aplicada às linhas digitadas. Confira o rastreio e salve.";
    }

    private Task UploadAsync(Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs e) => Execute(async () =>
    {
        if (!Editable || !confirmReplace) throw new ArgumentException("Confirme a substituição da grade antes de enviar.");
        try
        {
            using var stream = e.File.OpenReadStream(5 * 1024 * 1024);
            var imported = await FileReader.ReadAsync(e.File.Name, stream, RequiredSamples);
            pasteText = string.Join("\n", imported.Select(x => string.Join(";", x)));
            PasteRows();
            if (error is null) message = "Arquivo aplicado à grade. Confira identificação, leituras e salve.";
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or FormatException)
        { throw new ArgumentException("Arquivo inválido ou excede 5 MB. Use o modelo CSV/XLSX de leituras.", ex); }
    });

    private void PasteRows()
    {
        error = message = null;
        try
        {
            if (!confirmReplace)
                throw new ArgumentException("Confirme a substituição da grade.");
            var lines = pasteText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 1 || lines.Length > RequiredSamples)
                throw new ArgumentException($"Cole de uma a {RequiredSamples} linhas, sem cabeçalho.");
            var parsed = lines
                .Select(
                    (line, i) =>
                    {
                        var values =
                            line.Contains('\t') ? line.Split('\t')
                            : line.Contains(';') ? line.Split(';')
                            : line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (values.Length != 3 || values.Any(v => !Parse(v).HasValue))
                            throw new ArgumentException(
                                $"Linha {i + 1}: informe três números válidos."
                            );
                        return values.Select(v => v.Trim()).ToArray();
                    }
                )
                .ToArray();
            rows = Enumerable
                .Range(1, RequiredSamples)
                .Select(i => new EntryRow
                {
                    Position = i,
                    Values = i <= parsed.Length ? parsed[i - 1] : ["", "", ""],
                    InstrumentId = instrumentId,
                    CollectedAt = collectedAt,
                    Shift = shift,
                    Lot = lot,
                })
                .ToList();
            confirmReplace = false;
            MarkDirty();
            message = "Colagem aplicada à grade. Confira e salve o rascunho.";
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
        }
    }

    private Task SearchSourcesAsync() =>
        Execute(async () =>
        {
            sources = await Service.SourcesAsync(
                dataset!.Id,
                LocalTime.ParseLocal(sourceFrom.ToString("yyyy-MM-dd") + "T00:00"),
                LocalTime.ParseLocal(sourceTo.AddDays(1).ToString("yyyy-MM-dd") + "T00:00"),
                sourceShift,
                sourceLot,
                user
            );
            sourceSelection.Clear();
            searched = true;
        });

    private void ToggleSource(int id, bool selected)
    {
        if (selected && sourceSelection.Count >= RequiredSamples)
        {
            error = $"Selecione no máximo {RequiredSamples} amostras.";
            return;
        }
        if (selected)
            sourceSelection.Add(id);
        else
            sourceSelection.Remove(id);
    }

    private void ApplySources()
    {
        error = message = null;
        if (!confirmReplace || sourceSelection.Count < 1 || sourceSelection.Count > RequiredSamples)
        {
            error = $"Selecione de uma a {RequiredSamples} amostras e confirme a substituição da grade.";
            return;
        }
        var selected = sources
            .Where(x => sourceSelection.Contains(x.Subgroup.Id))
            .Select(x => x.Subgroup)
            .OrderBy(x => x.CollectedAt)
            .ThenBy(x => x.Id)
            .ToArray();
        rows = Enumerable
            .Range(1, RequiredSamples)
            .Select(i =>
            {
                if (i > selected.Length)
                    return new EntryRow
                    {
                        Position = i,
                        CollectedAt = collectedAt,
                        Shift = shift,
                    };
                var s = selected[i - 1];
                return new EntryRow
                {
                    Position = i,
                    Values = s
                        .Measurements.OrderBy(x => x.Position)
                        .Select(x => Raw(x.Value))
                        .ToArray(),
                    InstrumentId = s.InstrumentId,
                    CollectedAt = LocalTime.ToLocal(s.CollectedAt).DateTime,
                    OriginalTimestamp = s.CollectedAt,
                    Shift = s.Shift ?? "",
                    Lot = s.Lot ?? "",
                    SourceId = s.Id,
                    Operator = s.OperatorId,
                };
            })
            .ToList();
        confirmReplace = false;
        MarkDirty();
        message = "Amostras vinculadas à grade em ordem cronológica. Confira e salve.";
    }

    private string InstrumentCode(int? id) =>
        instruments.FirstOrDefault(x => x.Id == id)?.Code ?? id?.ToString() ?? "—";

    private static double? Parse(string? s) =>
        double.TryParse(
            s?.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var x
        ) && double.IsFinite(x)
            ? x
            : null;

    private static string Raw(double? value) =>
        value?.ToString("R", CultureInfo.InvariantCulture) ?? "";

    private static string Number(double? value) =>
        value?.ToString("0.####", CultureInfo.GetCultureInfo("pt-BR")) ?? "—";

    private static string ActionName(string action) =>
        action switch
        {
            "cep.create" => "Criação do rascunho",
            "cep.save" => "Rascunho salvo",
            "cep.complete" => "Conclusão da coleta",
            _ => action,
        };

    private static string Pretty(string? json) =>
        json is null
            ? "—"
            : JsonSerializer.Serialize(
                JsonDocument.Parse(json).RootElement,
                new JsonSerializerOptions { WriteIndented = true }
            );

    private sealed class EntryRow
    {
        public int Position { get; set; }
        public string[] Values { get; set; } = ["", "", ""];
        public int? InstrumentId { get; set; }
        public DateTime CollectedAt { get; set; }
        public DateTimeOffset? OriginalTimestamp { get; set; }
        public string Shift { get; set; } = "T1";
        public string Lot { get; set; } = "";
        public int? SourceId { get; set; }
        public string Operator { get; set; } = "";
        public bool Complete => Values.All(v => Parse(v).HasValue);
        public double? Mean => Complete ? Values.Sum(v => Parse(v)!.Value / 3) : null;
        public double? Range =>
            Complete ? Values.Max(v => Parse(v)!.Value) - Values.Min(v => Parse(v)!.Value) : null;
    }
}
