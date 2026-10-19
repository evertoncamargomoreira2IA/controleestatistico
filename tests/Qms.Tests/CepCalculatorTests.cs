using Qms.Statistics;

namespace Qms.Tests;

public sealed class CepCalculatorTests
{
    private static CepReading[] Series(
        Func<int, double> mean,
        Func<int, double>? halfRange = null
    ) =>
        Enumerable
            .Range(1, 25)
            .Select(i => new CepReading(
                i,
                mean(i) - (halfRange?.Invoke(i) ?? 1),
                mean(i),
                mean(i) + (halfRange?.Invoke(i) ?? 1)
            ))
            .ToArray();

    [Fact]
    public void Reference_25x3_limits_and_precision_match_independent_arithmetic()
    {
        var r = CepCalculator.Calculate(Series(_ => 10), 10, 5, 15);
        Assert.Equal(10, r.Mean, 12);
        Assert.Equal(2, r.AverageRange, 12);
        Assert.Equal(7.954, r.XLower, 12);
        Assert.Equal(12.046, r.XUpper, 12);
        Assert.Equal(0, r.RLower);
        Assert.Equal(5.148, r.RUpper, 12);
        Assert.Empty(r.Signals);
        Assert.Equal(25, r.Points.Count);
        Assert.Equal(r, r with { });
    }

    [Fact]
    public void Detects_shift_and_variability_spike_separately()
    {
        var shift = CepCalculator.Calculate(Series(i => i == 25 ? 20 : 10));
        Assert.Contains(shift.Signals, x => x.Code == "x-outside" && x.From == 25);
        Assert.DoesNotContain(shift.Signals, x => x.Code == "r-outside");
        var spread = CepCalculator.Calculate(Series(_ => 10, i => i == 12 ? 10 : 1));
        Assert.Contains(spread.Signals, x => x.Code == "r-outside" && x.From == 12);
        Assert.DoesNotContain(spread.Signals, x => x.Code == "x-outside");
    }

    [Theory]
    [InlineData(true, "seven-up")]
    [InlineData(false, "seven-down")]
    public void Seven_points_require_strict_trend_on_correct_side(bool increasing, string code)
    {
        double Mean(int i) => i <= 7 ? (increasing ? i : -i) : 0;
        var r = CepCalculator.Calculate(Series(Mean), 0);
        Assert.Contains(r.Signals, x => x.Code == code && x.From == 1 && x.To == 7);
        var equal = Series(Mean);
        equal[3] = equal[3] with
        {
            First = equal[2].First,
            Second = equal[2].Second,
            Third = equal[2].Third,
        };
        Assert.DoesNotContain(CepCalculator.Calculate(equal, 0).Signals, x => x.Code == code);
        Assert.DoesNotContain(CepCalculator.Calculate(Series(Mean)).Signals, x => x.Code == code);
        Assert.DoesNotContain(
            CepCalculator.Calculate(Series(i => i <= 7 ? i - 4 : 0), 0).Signals,
            x => x.Code == code
        );
    }

    [Fact]
    public void Specification_counts_individuals_and_means_and_accepts_boundaries()
    {
        var r = CepCalculator.Calculate(Series(_ => 0), 0, -1, 1);
        Assert.Equal(0, r.IndividualOutsideSpec);
        var s = Series(_ => 0);
        s[0] = new(1, -2, 0, 2);
        var separate = CepCalculator.Calculate(s, 0, -1, 1);
        Assert.Equal(2, separate.IndividualOutsideSpec);
        Assert.Equal(0, separate.MeansOutsideSpec);
        Assert.Equal(1, CepCalculator.Calculate(s, 0, null, 1).IndividualOutsideSpec);
    }

    [Fact]
    public void Zero_range_is_explicit_and_negative_values_are_supported()
    {
        var r = CepCalculator.Calculate(Series(_ => -5.25, _ => 0), -5.25, -6, -4.5);
        Assert.True(r.ZeroRange);
        Assert.Equal(r.Mean, r.XLower);
        Assert.Empty(r.Signals);
        var varied = CepCalculator.Calculate(Series(i => i % 2 == 0 ? -5.1 : -5.2, _ => 0));
        Assert.True(varied.ZeroRange);
        Assert.Equal(25, varied.Signals.Count);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_values_rejected(double value)
    {
        var rows = Series(_ => 10);
        rows[0] = rows[0] with { First = value };
        Assert.Throws<ArgumentException>(() => CepCalculator.Calculate(rows));
    }

    [Fact]
    public void Invalid_positions_limits_and_overflow_are_rejected()
    {
        var rows = Series(_ => 10);
        Assert.Throws<ArgumentException>(() => CepCalculator.Calculate(rows[..24]));
        rows[1] = rows[1] with { Position = 1 };
        Assert.Throws<ArgumentException>(() => CepCalculator.Calculate(rows));
        Assert.Throws<ArgumentException>(() =>
            CepCalculator.Calculate(Series(_ => 10), null, 5, 5)
        );
        Assert.Throws<ArgumentException>(() =>
            CepCalculator.Calculate(Series(_ => 10), double.NaN)
        );
        var huge = Series(_ => 0);
        huge[0] = new(1, -double.MaxValue, 0, double.MaxValue);
        Assert.Throws<ArgumentException>(() => CepCalculator.Calculate(huge));
    }
}
