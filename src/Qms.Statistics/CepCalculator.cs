namespace Qms.Statistics;

public sealed record CepReading(int Position, double First, double Second, double Third);

public sealed record CepPoint(int Position, double Mean, double Range);

public sealed record CepSignal(string Code, string Chart, int From, int To, string Description);

public sealed record CepResult(
    string Method,
    double Mean,
    double AverageRange,
    double XLower,
    double XUpper,
    double RLower,
    double RUpper,
    double? Nominal,
    double? Lsl,
    double? Usl,
    IReadOnlyList<CepPoint> Points,
    IReadOnlyList<CepSignal> Signals,
    int IndividualOutsideSpec,
    int MeansOutsideSpec,
    bool ZeroRange
);

public static class CepCalculator
{
    public const string Method = "xbar-r-25x3-v1";
    public const double A2 = 1.023,
        D3 = 0,
        D4 = 2.574;

    public static CepResult Calculate(
        IReadOnlyList<CepReading> readings,
        double? nominal = null,
        double? lsl = null,
        double? usl = null
    )
    {
        if (
            readings.Count != 25
            || !readings.Select(x => x.Position).Order().SequenceEqual(Enumerable.Range(1, 25))
        )
            throw new ArgumentException("A análise exige 25 posições únicas, de 1 a 25.");
        if (
            new[] { nominal, lsl, usl }.Any(x => x.HasValue && !double.IsFinite(x.Value))
            || (lsl.HasValue && usl.HasValue && lsl >= usl)
        )
            throw new ArgumentException("Limites de especificação inválidos.");
        var ordered = readings.OrderBy(x => x.Position).ToArray();
        if (
            ordered.Any(x =>
                !double.IsFinite(x.First) || !double.IsFinite(x.Second) || !double.IsFinite(x.Third)
            )
        )
            throw new ArgumentException("As 75 leituras devem ser números finitos.");
        var points = ordered
            .Select(x => new CepPoint(
                x.Position,
                x.First / 3 + x.Second / 3 + x.Third / 3,
                Math.Max(x.First, Math.Max(x.Second, x.Third))
                    - Math.Min(x.First, Math.Min(x.Second, x.Third))
            ))
            .ToArray();
        var mean = points.Sum(x => x.Mean / 25);
        var range = points.Sum(x => x.Range / 25);
        var xl = mean - A2 * range;
        var xu = mean + A2 * range;
        var ru = D4 * range;
        if (
            points.Any(x => !double.IsFinite(x.Mean) || !double.IsFinite(x.Range))
            || new[] { mean, range, xl, xu, ru }.Any(x => !double.IsFinite(x))
        )
            throw new ArgumentException(
                "Magnitude das leituras excede a capacidade numérica do método."
            );
        var signals = new List<CepSignal>();
        foreach (var p in points)
        {
            if (p.Mean < xl || p.Mean > xu)
                signals.Add(
                    new(
                        "x-outside",
                        "X̄",
                        p.Position,
                        p.Position,
                        "Média fora dos limites de controle"
                    )
                );
            if (p.Range > ru)
                signals.Add(
                    new(
                        "r-outside",
                        "R",
                        p.Position,
                        p.Position,
                        "Amplitude fora dos limites de controle"
                    )
                );
        }
        // Project-specific rule: seven strictly monotonic means, all on the specified side of nominal.
        if (nominal.HasValue)
            for (var i = 0; i <= points.Length - 7; i++)
            {
                var window = points.Skip(i).Take(7).ToArray();
                if (
                    window.All(x => x.Mean > nominal)
                    && window.Zip(window.Skip(1)).All(x => x.First.Mean < x.Second.Mean)
                )
                    signals.Add(
                        new(
                            "seven-up",
                            "X̄",
                            i + 1,
                            i + 7,
                            "Sete médias crescentes acima do nominal"
                        )
                    );
                if (
                    window.All(x => x.Mean < nominal)
                    && window.Zip(window.Skip(1)).All(x => x.First.Mean > x.Second.Mean)
                )
                    signals.Add(
                        new(
                            "seven-down",
                            "X̄",
                            i + 1,
                            i + 7,
                            "Sete médias decrescentes abaixo do nominal"
                        )
                    );
            }
        bool Outside(double x) => (lsl.HasValue && x < lsl) || (usl.HasValue && x > usl);
        return new(
            Method,
            mean,
            range,
            xl,
            xu,
            0,
            ru,
            nominal,
            lsl,
            usl,
            points,
            signals,
            ordered.SelectMany(x => new[] { x.First, x.Second, x.Third }).Count(Outside),
            points.Count(x => Outside(x.Mean)),
            range == 0
        );
    }
}
