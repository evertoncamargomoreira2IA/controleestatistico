using Qms.Domain.MasterData;

namespace Qms.Application.Collection;

public static class LocalTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "UTC-03",
                    TimeSpan.FromHours(-3),
                    "UTC-03",
                    "UTC-03"
                );
            }
        }
    }

    public static DateTimeOffset ParseLocal(string text)
    {
        if (
            !DateTime.TryParseExact(
                text,
                ["yyyy-MM-dd'T'HH:mm", "dd/MM/yyyy HH:mm"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var value
            )
        )
            throw new ArgumentException("Informe uma data e hora válidas.");
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
                Zone
            )
        );
    }

    public static DateTimeOffset ToLocal(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Zone);

    public static string Format(DateTimeOffset value) =>
        ToLocal(value)
            .ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record ValidationResult(double[] Parsed, IReadOnlyList<string> Errors);

public static class SubgroupValidator
{
    public static ValidationResult Validate(
        Characteristic characteristic,
        IReadOnlyList<object?> rawValues,
        Instrument? instrument,
        DateTimeOffset collectedAtUtc,
        DateTimeOffset nowUtc
    )
    {
        var parsed = new List<double>();
        var errors = new List<string>();
        if (rawValues.Count != characteristic.SubgroupSize)
            errors.Add($"O subgrupo deve conter exatamente {characteristic.SubgroupSize} valores.");
        for (var i = 0; i < rawValues.Count; i++)
        {
            try
            {
                var raw = rawValues[i];
                var text = raw is null
                    ? ""
                    : Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)
                        ?? "";
                if (raw is string)
                    text = text.Trim().Replace(',', '.');
                if (
                    !double.TryParse(
                        text,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var value
                    ) || !double.IsFinite(value)
                )
                    throw new FormatException();
                parsed.Add(value);
            }
            catch (Exception ex)
                when (ex is FormatException or OverflowException or InvalidCastException)
            {
                errors.Add($"Valor na posição {i + 1} está vazio ou não é um número finito.");
            }
        }
        if (!characteristic.IsActive)
            errors.Add("A característica está inativa e não aceita coleta.");
        if (characteristic.DataType != CharacteristicDataType.Continuous)
            errors.Add("Esta fase aceita coleta apenas para características contínuas.");
        if (instrument is null)
            errors.Add("O instrumento é obrigatório.");
        else
        {
            if (!instrument.IsActive)
                errors.Add("O instrumento está inativo e não pode ser usado na coleta.");
            if (
                instrument.CalibrationDue is not null
                && DateOnly.FromDateTime(collectedAtUtc.UtcDateTime) > instrument.CalibrationDue
            )
                errors.Add("A calibração do instrumento venceu na data informada para a coleta.");
        }
        if (collectedAtUtc > nowUtc.AddMinutes(5))
            errors.Add("A data e hora da coleta não pode estar mais de 5 minutos no futuro.");
        return new(parsed.ToArray(), errors);
    }
}

public sealed record CollectionInput(
    int CharacteristicId,
    IReadOnlyList<object?> Values,
    int? InstrumentId,
    DateTimeOffset CollectedAt,
    string? Shift,
    string? Lot,
    string? Note
);

public interface ICollectionService
{
    Task<IReadOnlyList<Qms.Domain.Collection.Subgroup>> ListRecentAsync(
        int characteristicId,
        int take = 20,
        CancellationToken cancellationToken = default
    );
    Task<Qms.Domain.Collection.Subgroup> SaveAsync(
        CollectionInput input,
        string actorId,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExcludeAsync(
        int subgroupId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default
    );
}

public interface IImportService
{
    Task<IReadOnlyList<Dictionary<string, object?>>> ParseAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<CollectionRow>> PreviewAsync(
        IReadOnlyList<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken = default
    );
    Task<Qms.Domain.Collection.ImportBatch> CommitAsync(
        string fileName,
        byte[] bytes,
        string actor,
        CancellationToken cancellationToken = default
    );
}

/// <summary>One parsed file row after server-side validation (preview and commit share it).</summary>
public sealed record CollectionRow(
    int Line,
    Characteristic? Characteristic,
    Instrument? Instrument,
    DateTimeOffset? CollectedAt,
    IReadOnlyList<string> RawValues,
    double[] Values,
    string? Shift,
    string? Lot,
    string? Note,
    IReadOnlyList<string> Errors
)
{
    public bool IsValid => Errors.Count == 0;
}

public interface ITemplateService
{
    byte[] BuildXlsx(Characteristic characteristic);
    byte[] BuildCsv(Characteristic characteristic);
}
