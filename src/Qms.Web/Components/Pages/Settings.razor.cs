using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Settings;
using Qms.Domain.Security;
using Qms.Web.Components.Ui;

namespace Qms.Web.Components.Pages;

public partial class Settings
{
    private SettingsForm settings = new(QualitySettings.Defaults);
    private IReadOnlyList<SettingsHistory> history = [];
    private Dictionary<string, string> authors = new();
    private bool canSave;
    private string comment = "";
    private string? error;

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        canSave = Roles.All.Any(role =>
            user.IsInRole(role) && Permissions.Allows(role, "settings.manage")
        );
        settings = new(await Service.GetActiveAsync());
        history = await Service.GetHistoryAsync();
        var ids = history.Select(x => x.CreatedBy).Where(x => x is not null).Distinct().ToArray();
        authors = await Db
            .Users.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName);
    }

    private string Author(string? id) => id is null ? "—" : authors.GetValueOrDefault(id, id);

    private BandForm GetBand(string key) =>
        key switch
        {
            "Cp" => settings.Cp,
            "Cpk" => settings.Cpk,
            "Pp" => settings.Pp,
            _ => settings.Ppk,
        };

    private static string N(double n) => n.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

    private async Task Save()
    {
        var actor = (
            await AuthenticationStateProvider.GetAuthenticationStateAsync()
        ).User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await Service.SaveAsync(settings.ToSettings(), comment, actor);
        error = result.Error;
        if (result.Succeeded)
        {
            comment = "";
            history = await Service.GetHistoryAsync();
            var ids = history
                .Select(x => x.CreatedBy)
                .Where(x => x is not null)
                .Distinct()
                .ToArray();
            authors = await Db
                .Users.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName);
        }
    }

    private sealed class BandForm(Band value)
    {
        public double BadBelow { get; set; } = value.BadBelow;
        public double GoodFrom { get; set; } = value.GoodFrom;

        public Band ToBand() => new(BadBelow, GoodFrom);
    }

    private sealed class RunForm(RunRule value)
    {
        public bool Enabled { get; set; } = value.Enabled;
        public int N { get; set; } = value.N;

        public RunRule ToRule() => new(Enabled, N);
    }

    private sealed class ProximityForm(ProximityRule value)
    {
        public bool Enabled { get; set; } = value.Enabled;
        public double Percent { get; set; } = value.Percent;

        public ProximityRule ToRule() => new(Enabled, Percent);
    }

    private sealed class SettingsForm(QualitySettings value)
    {
        public BandForm Cp { get; } = new(value.Cp);
        public BandForm Cpk { get; } = new(value.Cpk);
        public BandForm Pp { get; } = new(value.Pp);
        public BandForm Ppk { get; } = new(value.Ppk);
        public bool Outside3Sigma { get; set; } = value.Outside3Sigma;
        public RunForm SameSide { get; } = new(value.SameSide);
        public RunForm Trend { get; } = new(value.Trend);
        public ProximityForm SpecProximity { get; } = new(value.SpecProximity);
        public int ControlLimitsDays { get; set; } = value.ControlLimitsDays;

        public QualitySettings ToSettings() =>
            new(
                Cp.ToBand(),
                Cpk.ToBand(),
                Pp.ToBand(),
                Ppk.ToBand(),
                Outside3Sigma,
                SameSide.ToRule(),
                Trend.ToRule(),
                SpecProximity.ToRule(),
                ControlLimitsDays
            );
    }
}
