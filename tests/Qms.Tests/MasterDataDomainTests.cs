using Qms.Domain.MasterData;

namespace Qms.Tests;

public sealed class MasterDataDomainTests
{
    [Fact]
    public void Validates_specification_and_chart_subgroup()
    {
        var item = new Characteristic { Lsl = 5, Usl = 4 };
        Assert.Equal("LSL deve ser menor que USL", item.Validate());
        item.Lsl = 1;
        item.Usl = 5;
        item.Nominal = 6;
        Assert.Equal("Nominal deve estar entre LSL e USL", item.Validate());
        item.Nominal = 3;
        item.SubgroupSize = 2;
        Assert.Equal("Tamanho do subgrupo incompatível com o tipo de carta", item.Validate());
        item.ChartType = ChartType.XbarR;
        Assert.Null(item.Validate());
        item.SubgroupSize = 11;
        Assert.NotNull(item.Validate());
        item.ChartType = ChartType.XbarS;
        item.SubgroupSize = 25;
        Assert.Null(item.Validate());
        item.SubgroupSize = 26;
        Assert.NotNull(item.Validate());
        // Attributes ignore the specification (LSL/nominal/USL) but, as in the Python
        // version, still need a subgroup size that matches the chart type.
        item.SubgroupSize = 25;
        item.Lsl = 9;
        item.Usl = 1;
        item.DataType = CharacteristicDataType.Attribute;
        Assert.Null(item.Validate());
    }

    [Fact]
    public void Specification_revision_changes_only_when_specification_changes()
    {
        var item = new Characteristic();
        item.UpdateSpecification(null, null, null, 1, ChartType.IMr);
        Assert.Equal(1, item.Revision);
        item.UpdateSpecification(1, 2, 3, 2, ChartType.XbarR);
        Assert.Equal(2, item.Revision);
        item.UpdateSpecification(1, 2, 3, 2, ChartType.XbarR);
        Assert.Equal(2, item.Revision);
    }

    [Fact]
    public void Instrument_resolution_must_be_positive()
    {
        Assert.Null(new Instrument { Resolution = null }.Validate());
        Assert.NotNull(new Instrument { Resolution = 0 }.Validate());
        Assert.NotNull(new Instrument { Resolution = -1 }.Validate());
    }
}
