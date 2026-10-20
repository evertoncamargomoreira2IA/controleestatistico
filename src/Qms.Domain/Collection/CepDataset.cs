namespace Qms.Domain.Collection;

public sealed class CepDataset
{
    public const int TargetSamples = 25;
    public string StudyKind { get; set; } = "CEP";
    public int RequiredSamples => StudyKind == "CAPABILITY" ? 33 : 25;
    public int Id { get; set; }
    public int CharacteristicId { get; set; }
    public int CharacteristicRevision { get; set; }
    public string CharacteristicName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public string? Unit { get; set; }
    public double? Lsl { get; set; }
    public double? Nominal { get; set; }
    public double? Usl { get; set; }
    public string Title { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Version { get; set; } = 1;
    public List<CepSample> Samples { get; set; } = [];
    public int CompleteSamples => Samples.Count(x => x.IsComplete);
    public int Readings =>
        Samples.Sum(x => new[] { x.Value1, x.Value2, x.Value3 }.Count(v => v.HasValue));
    public int ProgressPercent => CompleteSamples * 100 / RequiredSamples;
}

public sealed class CepSample
{
    public int Id { get; set; }
    public int CepDatasetId { get; set; }
    public int Position { get; set; }
    public double? Value1 { get; set; }
    public double? Value2 { get; set; }
    public double? Value3 { get; set; }
    public int? InstrumentId { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public string Shift { get; set; } = "";
    public string Lot { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public string Source { get; set; } = "frontend";
    public int? SourceSubgroupId { get; set; }
    public bool IsComplete => Value1.HasValue && Value2.HasValue && Value3.HasValue;
    public double? Mean =>
        IsComplete ? Value1!.Value / 3 + Value2!.Value / 3 + Value3!.Value / 3 : null;
    public double? Range =>
        IsComplete
            ? new[] { Value1!.Value, Value2!.Value, Value3!.Value }.Max()
                - new[] { Value1!.Value, Value2!.Value, Value3!.Value }.Min()
            : null;
}
