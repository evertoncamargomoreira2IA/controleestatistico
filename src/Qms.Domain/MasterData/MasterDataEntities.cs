namespace Qms.Domain.MasterData;

public abstract class MasterDataEntity
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Area : MasterDataEntity
{
    public string Name { get; set; } = "";
}

public sealed class Product : MasterDataEntity
{
    public int AreaId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Line : MasterDataEntity
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Station : MasterDataEntity
{
    public int LineId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Tooling : MasterDataEntity
{
    public int StationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public enum CharacteristicDataType
{
    Continuous,
    Attribute,
}

public enum ChartType
{
    XbarR,
    XbarS,
    IMr,
}

public sealed class Characteristic : MasterDataEntity
{
    public int ToolingId { get; set; }
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public double? Lsl { get; set; }
    public double? Nominal { get; set; }
    public double? Usl { get; set; }
    public CharacteristicDataType DataType { get; set; } = CharacteristicDataType.Continuous;
    public int SubgroupSize { get; set; } = 1;
    public ChartType ChartType { get; set; } = ChartType.IMr;
    public int Revision { get; private set; } = 1;

    public string? Validate()
    {
        if (DataType != CharacteristicDataType.Attribute)
        {
            if (Lsl.HasValue && Usl.HasValue && Lsl >= Usl)
                return "LSL deve ser menor que USL";
            if (
                Nominal.HasValue
                && ((Lsl.HasValue && Nominal < Lsl) || (Usl.HasValue && Nominal > Usl))
            )
                return "Nominal deve estar entre LSL e USL";
        }
        if (
            ChartType == ChartType.IMr && SubgroupSize != 1
            || ChartType == ChartType.XbarR && SubgroupSize is < 2 or > 10
            || ChartType == ChartType.XbarS && SubgroupSize is < 2 or > 25
        )
            return "Tamanho do subgrupo incompatível com o tipo de carta";
        return null;
    }

    public void UpdateSpecification(
        double? lsl,
        double? nominal,
        double? usl,
        int subgroupSize,
        ChartType chartType
    )
    {
        var changed =
            Lsl != lsl
            || Nominal != nominal
            || Usl != usl
            || SubgroupSize != subgroupSize
            || ChartType != chartType;
        Lsl = DataType == CharacteristicDataType.Attribute ? null : lsl;
        Nominal = DataType == CharacteristicDataType.Attribute ? null : nominal;
        Usl = DataType == CharacteristicDataType.Attribute ? null : usl;
        SubgroupSize = subgroupSize;
        ChartType = chartType;
        if (changed)
            Revision++;
    }
}

public sealed class Instrument : MasterDataEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "";
    public double? Resolution { get; set; }
    public DateOnly? CalibrationDue { get; set; }

    public string? Validate() => Resolution is <= 0 ? "A resolução deve ser maior que zero." : null;
}
