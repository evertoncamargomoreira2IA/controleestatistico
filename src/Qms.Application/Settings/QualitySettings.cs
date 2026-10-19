using System.Globalization;

namespace Qms.Application.Settings;

public sealed record Band(double BadBelow, double GoodFrom);

public sealed record RunRule(bool Enabled, int N);

public sealed record ProximityRule(bool Enabled, double Percent);

public sealed record QualitySettings(
    Band Cp,
    Band Cpk,
    Band Pp,
    Band Ppk,
    bool Outside3Sigma,
    RunRule SameSide,
    RunRule Trend,
    ProximityRule SpecProximity,
    int ControlLimitsDays
)
{
    public static QualitySettings Defaults { get; } =
        new(
            new(1, 1.33),
            new(1, 1.33),
            new(1, 1.33),
            new(1, 1.33),
            true,
            new(true, 9),
            new(true, 6),
            new(false, 95),
            180
        );

    public string? Validate()
    {
        foreach (var (name, band) in new[] { ("CP", Cp), ("CPK", Cpk), ("PP", Pp), ("PPK", Ppk) })
            if (band.BadBelow >= band.GoodFrom)
                return $"Em {name}, o limite ruim deve ser menor que o bom.";
        if (SameSide.N < 2 || Trend.N < 2)
            return "Os valores inteiros devem respeitar o mínimo permitido.";
        if (new[] { SpecProximity.Percent }.Any(x => x is < 0 or > 100))
            return "Percentuais devem estar entre 0 e 100.";
        if (ControlLimitsDays < 1)
            return "A frequência deve ser de pelo menos 1 dia.";
        return null;
    }
}

public enum Classification
{
    Good,
    Attention,
    Bad,
}

public static class Classifier
{
    public static Classification? Classify(
        string metric,
        double? value,
        QualitySettings? settings = null
    )
    {
        if (value is null)
            return null;
        var s = settings ?? QualitySettings.Defaults;
        var key = metric.ToLowerInvariant().Replace("%", "").Replace(" ", "_");
        if (key is "cp" or "capability.cp")
            return Apply(value.Value, s.Cp);
        if (key is "cpk" or "capability.cpk")
            return Apply(value.Value, s.Cpk);
        if (key is "pp" or "capability.pp")
            return Apply(value.Value, s.Pp);
        if (key is "ppk" or "capability.ppk")
            return Apply(value.Value, s.Ppk);
        return null;
    }

    private static Classification Apply(double value, Band band) =>
        value >= band.GoodFrom ? Classification.Good
        : value < band.BadBelow ? Classification.Bad
        : Classification.Attention;
}

public static class SettingsPresentation
{
    public static IReadOnlyDictionary<string, string> Labels { get; } =
        new Dictionary<string, string>
        {
            ["capability.cp"] = "Cp",
            ["capability.cpk"] = "Cpk",
            ["capability.pp"] = "Pp",
            ["capability.ppk"] = "Ppk",
            ["chart.outside_3sigma"] = "Fora de 3σ",
            ["chart.same_side"] = "Regra: N pontos do mesmo lado",
            ["chart.trend"] = "Regra: tendência",
            ["chart.spec_proximity"] = "Proximidade da especificação",
            ["frequency.control_limits_days"] = "Dias de recálculo dos limites",
        };

    public static string Describe(string key, object? value)
    {
        if (value is null)
            return "—";
        static string N(double n) => n.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
        if (value is Band b)
            return $"ruim abaixo de {N(b.BadBelow)}; bom a partir de {N(b.GoodFrom)}";
        if (value is RunRule r)
            return $"{(r.Enabled ? "ativa" : "inativa")}, N = {r.N}";
        if (value is ProximityRule q)
            return $"{(q.Enabled ? "ativa" : "inativa")}, {N(q.Percent)}%";
        if (value is bool flag)
            return flag ? "sim" : "não";
        if (value is double d)
            return N(d);
        return Convert.ToString(value, CultureInfo.GetCultureInfo("pt-BR")) ?? "";
    }
}
