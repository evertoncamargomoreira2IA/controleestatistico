using Qms.Application.Settings;

namespace Qms.Tests;

public sealed class QualitySettingsTests
{
    [Theory]
    [InlineData("cpk", 1.33, Classification.Good)]
    [InlineData("cpk", 1.0, Classification.Attention)]
    [InlineData("cpk", 0.9999, Classification.Bad)]
    public void Classifies_boundary_values(string metric, double value, Classification expected) =>
        Assert.Equal(expected, Classifier.Classify(metric, value));

    [Fact]
    public void Validates_custom_settings_and_uses_custom_thresholds()
    {
        var custom = QualitySettings.Defaults with { Cpk = new(.8, 1.5) };
        Assert.Equal(Classification.Attention, Classifier.Classify("cpk", 1.2, custom));
        Assert.Equal(
            "Em CPK, o limite ruim deve ser menor que o bom.",
            (custom with { Cpk = new(1.5, 1.5) }).Validate()
        );
    }
}
