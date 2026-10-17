using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Qms.Application.MasterData;
using Qms.Domain.MasterData;
using Qms.Domain.Security;
using Qms.Web.Components.Ui;

namespace Qms.Web.Components.Pages;

public partial class Instruments
{
    private IReadOnlyList<Instrument> instruments = [];
    private Instrument model = new();
    private Instrument? selected;
    private DateOnly? due;
    private bool open,
        canManage;
    private string? error;

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        canManage = Roles.All.Any(role =>
            user.IsInRole(role) && Permissions.Allows(role, "masterdata.manage")
        );
        await Load();
    }

    private async Task Load() => instruments = await Service.ListAsync();

    private void New()
    {
        selected = null;
        model = new();
        due = null;
        error = null;
        open = true;
    }

    private void Edit(Instrument x)
    {
        selected = x;
        model = new()
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Type = x.Type,
            Resolution = x.Resolution,
            Description = x.Description,
            IsActive = x.IsActive,
        };
        due = x.CalibrationDue;
        error = null;
        open = true;
    }

    private async Task Save()
    {
        var actor = (
            await AuthenticationStateProvider.GetAuthenticationStateAsync()
        ).User.FindFirstValue(ClaimTypes.NameIdentifier);
        model.CalibrationDue = due;
        var result = await Service.SaveAsync(model, actor);
        error = result.Error;
        if (result.Succeeded)
        {
            open = false;
            await Load();
        }
    }

    private static StatusChip.ChipStatus DueStatus(Instrument x) =>
        x.CalibrationDue is null ? StatusChip.ChipStatus.Neutral
        : x.CalibrationDue < DateOnly.FromDateTime(DateTime.Today) ? StatusChip.ChipStatus.Bad
        : x.CalibrationDue <= DateOnly.FromDateTime(DateTime.Today).AddDays(30)
            ? StatusChip.ChipStatus.Warn
        : StatusChip.ChipStatus.Ok;

    private static string DueText(Instrument x) =>
        x.CalibrationDue is null ? "Sem vencimento"
        : x.CalibrationDue < DateOnly.FromDateTime(DateTime.Today) ? "Vencida"
        : x.CalibrationDue <= DateOnly.FromDateTime(DateTime.Today).AddDays(30)
            ? $"Vence em {x.CalibrationDue.Value.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber} dias"
        : "Em dia";
}
