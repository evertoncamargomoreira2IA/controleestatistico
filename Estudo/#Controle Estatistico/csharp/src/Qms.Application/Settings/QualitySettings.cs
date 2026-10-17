using System.Globalization;

namespace Qms.Application.Settings;

public sealed record Band(double BadBelow, double GoodFrom);

public sealed record PercentBand(double GoodMax, double BadAbove);

public sealed record RunRule(bool Enabled, int N);

public sealed record ProximityRule(bool Enabled, double Percent);

public sealed record QualitySettings(
    Band Cp,
    Band Cpk,
    Band Pp,
    Band Ppk,
    PercentBand StudyPercent,
    PercentBand TolerancePercent,
    int NdcMin,
    Band Kappa,
    double EffectivenessMin,
    double MissMax,
    double FalseAlarmMax,
    bool Outside3Sigma,
    RunRule SameSide,
    RunRule Trend,
    ProximityRule SpecProximity,
    int ControlLimitsDays,
    int MsaValidityDays
)
{
    public static QualitySettings Defaults { get; } =
        new(
            new(1, 1.33),
            new(1, 1.33),
            new(1, 1.33),
            new(1, 1.33),
            new(10, 30),
            new(10, 30),
            5,
            new(.75, .9),
            90,
            2,
            5,
            true,
            new(true, 9),
            new(true, 6),
            new(false, 95),
            180,
            365
        );

    public string? Validate()
    {
        foreach (var (name, band) in new[] { ("CP", Cp), ("CPK", Cpk), ("PP", Pp), ("PPK", Ppk) })
            if (band.BadBelow >= band.GoodFrom)
                return $"Em {name}, o limite ruim deve ser menor que o bom.";
        if (Kappa.BadBelow >= Kappa.GoodFrom)
            return "Em Kappa, o limite ruim deve ser menor que o bom.";
        if (!ValidPercent(StudyPercent) || !ValidPercent(TolerancePercent))
            return "Percentuais Gage R&R devem estar entre 0 e 100, com bom menor que ruim.";
        if (NdcMin < 1 || SameSide.N < 2 || Trend.N < 2)
            return "Os valores inteiros devem respeitar o mínimo permitido.";
        if (
            new[] { EffectivenessMin, MissMax, FalseAlarmMax, SpecProximity.Percent }.Any(x =>
                x is < 0 or > 100
            )
        )
            return "Percentuais devem estar entre 0 e 100.";
        if (ControlLimitsDays < 1 || MsaValidityDays < 1)
            return "A frequência deve ser de pelo menos 1 dia.";
        return null;
    }

    private static bool ValidPercent(PercentBand value) =>
        value.GoodMax >= 0 && value.GoodMax < value.BadAbove && value.BadAbove <= 100;
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
        if (key is "gage_rr_study" or "gage_rr.study_percent")
            return Apply(value.Value, s.StudyPercent);
        if (key is "gage_rr_tolerance" or "gage_rr.tolerance_percent")
            return Apply(value.Value, s.TolerancePercent);
        if (key is "kappa" or "kappa.kappa")
            return Apply(value.Value, s.Kappa);
        return null;
    }

    private static Classification Apply(double value, Band band) =>
        value >= band.GoodFrom ? Classification.Good
        : value < band.BadBelow ? Classification.Bad
        : Classification.Attention;

    private static Classification Apply(double value, PercentBand band) =>
        value <= band.GoodMax ? Classification.Good
        : value > band.BadAbove ? Classification.Bad
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
            ["gage_rr.study_percent"] = "%R&R do estudo",
            ["gage_rr.tolerance_percent"] = "%R&R da tolerância",
            ["gage_rr.ndc_min"] = "Ndc mínimo",
            ["kappa.kappa"] = "Kappa",
            ["kappa.effectiveness_min"] = "Efetividade mínima (%)",
            ["kappa.miss_max"] = "Taxa máxima de deixar passar (%)",
            ["kappa.false_alarm_max"] = "Taxa máxima de alarme falso (%)",
            ["chart.outside_3sigma"] = "Fora de 3σ",
            ["chart.same_side"] = "Regra: N pontos do mesmo lado",
            ["chart.trend"] = "Regra: tendência",
            ["chart.spec_proximity"] = "Proximidade da especificação",
            ["frequency.control_limits_days"] = "Dias de recálculo dos limites",
            ["frequency.msa_validity_days"] = "Validade do MSA (dias)",
        };

    public static string Describe(string key, object? value)
    {
        if (value is null)
            return "—";
        static string N(double n) => n.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
        if (value is Band b)
            return $"ruim abaixo de {N(b.BadBelow)}; bom a partir de {N(b.GoodFrom)}";
        if (value is PercentBand p)
            return $"bom até {N(p.GoodMax)}%; ruim acima de {N(p.BadAbove)}%";
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
