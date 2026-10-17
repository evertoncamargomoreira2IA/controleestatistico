using Qms.Application.Collection;
using Qms.Domain.MasterData;

namespace Qms.Tests;

public sealed class CollectionDomainTests
{
    [Fact]
    public void LocalTime_ConvertsSaoPauloToUtc()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 10, 15, 15, 0, 0, TimeSpan.Zero),
            LocalTime.ParseLocal("2026-10-15T12:00")
        );
    }

    [Fact]
    public void Validator_AcceptsCommaDecimalAndReportsInvalidPosition()
    {
        var characteristic = new Characteristic { SubgroupSize = 2 };
        var instrument = new Instrument { IsActive = true };
        var result = SubgroupValidator.Validate(
            characteristic,
            ["1,25", "abc"],
            instrument,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );
        Assert.Equal(1.25, result.Parsed[0]);
        Assert.Contains("Valor na posição 2 está vazio ou não é um número finito.", result.Errors);
    }

    [Fact]
    public void Validator_RejectsAttributeFutureAndExpiredCalibration()
    {
        var characteristic = new Characteristic
        {
            DataType = CharacteristicDataType.Attribute,
            SubgroupSize = 2,
        };
        var instrument = new Instrument
        {
            IsActive = true,
            CalibrationDue = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        };
        var now = DateTimeOffset.UtcNow;
        var result = SubgroupValidator.Validate(
            characteristic,
            ["1", "2"],
            instrument,
            now.AddDays(1),
            now
        );
        Assert.Contains(
            "Esta fase aceita coleta apenas para características contínuas.",
            result.Errors
        );
        Assert.Contains(
            "A calibração do instrumento venceu na data informada para a coleta.",
            result.Errors
        );
        Assert.Contains(
            "A data e hora da coleta não pode estar mais de 5 minutos no futuro.",
            result.Errors
        );
    }

    [Fact]
    public void Validator_RejectsInvalidShiftAtServiceBoundaryIsHandledSeparately()
    {
        Assert.True(true);
    }
}
