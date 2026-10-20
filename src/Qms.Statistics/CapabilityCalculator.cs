namespace Qms.Statistics;

public sealed record CapabilityResult(
    string Method, double Mean, double AverageMovingRange, double SigmaWithinMeans,
    double Lsl, double Usl, double Cp, double Cpu, double Cpl, double Cpk,
    IReadOnlyList<CepPoint> Points, int MeansOutsideSpec, int IndividualOutsideSpec,
    IReadOnlyList<string> StabilitySignals);

public static class CapabilityCalculator
{
    // The 33 trio means are individual observations. Estimate local variation of that
    // ordered series using adjacent moving ranges (span 2), never the 99 raw values.
    public const string Method = "cp-cpk-33means-mr2-v1";
    public const double D2 = 1.128;

    public static CapabilityResult Calculate(IReadOnlyList<CepReading> readings, double? lsl, double? usl)
    {
        if (readings.Count != 33 || !readings.Select(x => x.Position).Order().SequenceEqual(Enumerable.Range(1, 33)))
            throw new ArgumentException("Cp/Cpk exige 33 amostras, com três leituras cada e posições de 1 a 33.");
        if (!lsl.HasValue || !usl.HasValue || !double.IsFinite(lsl.Value) || !double.IsFinite(usl.Value) || lsl >= usl)
            throw new ArgumentException("Informe os dois limites de especificação, com LSL menor que USL.");
        var ordered = readings.OrderBy(x => x.Position).ToArray();
        if (ordered.Any(x => !double.IsFinite(x.First) || !double.IsFinite(x.Second) || !double.IsFinite(x.Third)))
            throw new ArgumentException("As 99 leituras devem ser números finitos.");
        var points = ordered.Select(x => new CepPoint(x.Position, x.First / 3 + x.Second / 3 + x.Third / 3,
            Math.Max(x.First, Math.Max(x.Second, x.Third)) - Math.Min(x.First, Math.Min(x.Second, x.Third)))).ToArray();
        var mean = points.Sum(x => x.Mean / 33);
        var ranges = points.Zip(points.Skip(1), (a, b) => Math.Abs(b.Mean - a.Mean)).ToArray();
        var mr = ranges.Sum(x => x / 32);
        var sigma = mr / D2;
        if (!double.IsFinite(sigma) || sigma <= 0)
            throw new ArgumentException("Variação das médias nula ou inválida: Cp/Cpk indisponível. Confira a resolução e a coleta.");
        var cp = (usl.Value - lsl.Value) / (6 * sigma);
        var cpu = (usl.Value - mean) / (3 * sigma);
        var cpl = (mean - lsl.Value) / (3 * sigma);
        if (new[] { mean, mr, cp, cpu, cpl }.Any(x => !double.IsFinite(x)) || points.Any(x => !double.IsFinite(x.Range)))
            throw new ArgumentException("Valores excedem a faixa numérica permitida.");
        var signals = new List<string>();
        if (points.Any(x => Math.Abs(x.Mean - mean) > 3 * sigma)) signals.Add("Média além de três sigmas da série de médias.");
        if (ranges.Any(x => x > 3.267 * mr)) signals.Add("Amplitude móvel além do limite de controle.");
        for (var i = 0; i <= points.Length - 7; i++)
        {
            var run = points.Skip(i).Take(7).Select(x => x.Mean).ToArray();
            if (run.All(x => x > mean) || run.All(x => x < mean)) { signals.Add("Sete médias consecutivas do mesmo lado do centro."); break; }
        }
        for (var i = 0; i <= points.Length - 7; i++)
        {
            var run = points.Skip(i).Take(7).Select(x => x.Mean).ToArray();
            if (run.Zip(run.Skip(1), (a, b) => b > a).All(x => x) || run.Zip(run.Skip(1), (a, b) => b < a).All(x => x))
            { signals.Add("Sete médias consecutivas em tendência estrita."); break; }
        }
        return new(Method, mean, mr, sigma, lsl.Value, usl.Value, cp, cpu, cpl, Math.Min(cpu, cpl), points,
            points.Count(x => x.Mean < lsl || x.Mean > usl),
            ordered.SelectMany(x => new[] { x.First, x.Second, x.Third }).Count(x => x < lsl || x > usl), signals);
    }
}
