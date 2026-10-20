using Qms.Statistics;

namespace Qms.Tests;

public sealed class CepOperationalRulesTests
{
    private static CepRulePolicy Policy => new(7, true, true, 9, true, 6, true, 95);

    private static CepReading[] Rows(Func<int, double> mean) =>
        Enumerable
            .Range(1, 25)
            .Select(i => new CepReading(i, mean(i) - .01, mean(i), mean(i) + .01))
            .ToArray();

    [Fact]
    public void Frozen_limits_keep_the_baseline_center_and_detect_a_shift()
    {
        var result = CepCalculator.EvaluateOperational(
            Rows(_ => 11),
            10,
            9,
            12,
            Policy,
            new(9.9, 10.1, 0, .1, 10, .02),
            3
        );
        Assert.Equal(11, result.Mean, 10);
        Assert.Equal(10, result.ControlCenter);
        Assert.Equal(10.1, result.XUpper);
        Assert.Equal(3, result.ApprovedLimitId);
        Assert.Equal(25, result.Signals.Count(x => x.Code == "x-outside"));
        Assert.Contains(result.Signals, x => x.Code == "same-side");
        Assert.Contains("s7-l3", result.Method);
    }

    [Fact]
    public void Rules_respect_switches_ties_and_proximity_definition()
    {
        var p = Policy with
        {
            Outside = false,
            SameSideEnabled = false,
            TrendEnabled = false,
            ProximityEnabled = false,
        };
        Assert.Empty(
            CepCalculator
                .EvaluateOperational(Rows(_ => 11), 10, 9, 12, p, new(9.9, 10.1, 0, .1, 10, .02))
                .Signals
        );
        var trend = CepCalculator.EvaluateOperational(
            Rows(i => 10 + i * .001),
            null,
            9,
            12,
            Policy
        );
        Assert.Contains(trend.Signals, x => x.Code == "trend");
        Assert.DoesNotContain(trend.Signals, x => x.Code == "seven-up");
        var prox = CepCalculator.EvaluateOperational(Rows(_ => 11.96), null, 10, 12, Policy);
        Assert.Equal(25, prox.Signals.Count(x => x.Code == "spec-proximity"));
        var oneSided = CepCalculator.EvaluateOperational(Rows(_ => 11.96), null, null, 12, Policy);
        Assert.DoesNotContain(oneSided.Signals, x => x.Code == "spec-proximity");
    }

    [Fact]
    public void Invalid_policy_and_limits_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CepCalculator.EvaluateOperational(Rows(_ => 10), 10, 9, 12, Policy with { TrendN = 26 })
        );
        Assert.Throws<ArgumentException>(() =>
            CepCalculator.EvaluateOperational(
                Rows(_ => 10),
                10,
                9,
                12,
                Policy,
                new(11, 9, 0, .1, 10, .02)
            )
        );
    }
}
