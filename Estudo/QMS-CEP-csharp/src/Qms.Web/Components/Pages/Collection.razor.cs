using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Collection;
using Qms.Application.MasterData;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
using Qms.Domain.Security;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;

namespace Qms.Web.Components.Pages;

public partial class Collection
{
    [Inject]
    private IMasterDataService MasterData { get; set; } = default!;

    [Inject]
    private IInstrumentService InstrumentService { get; set; } = default!;

    [Inject]
    private ICollectionService CollectionService { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    [Inject]
    private QmsDbContext Db { get; set; } = default!;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    private readonly string[] labels =
    [
        "Área",
        "Produto",
        "Linha",
        "Posto",
        "Tooling",
        "Característica",
    ];
    private readonly List<MasterDataEntity>[] items =
    [
        [],
        [],
        [],
        [],
        [],
        [],
    ];
    private readonly MasterDataEntity?[] selected = new MasterDataEntity?[6];
    private IReadOnlyList<Instrument> instruments = [];
    private IReadOnlyList<Subgroup> recent = [];
    private Dictionary<int, string> instrumentNames = [];
    private Dictionary<string, string> operatorNames = [];
    private ElementReference[] valueRefs = [];
    private string[] values = [];
    private int? instrumentId;
    private int? excludeId;

    // Wall-clock time in the plant (America/Sao_Paulo); converted to UTC on save.
    private DateTime collectedAt = DateTime.Now;
    private string selectedShift = "T1";
    private string lot = "";
    private string note = "";
    private string excludeReason = "";
    private string? error;
    private string? success;
    private bool canCreate;
    private bool canManage;
    private DateOnly todayLocal =>
        DateOnly.FromDateTime(LocalTime.ToLocal(DateTimeOffset.UtcNow).DateTime);

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        canCreate = HasPermission(user, "collection.create");
        canManage = HasPermission(user, "masterdata.manage");
        await LoadLevel(0, 0);
        instruments = (await InstrumentService.ListAsync())
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToArray();
        ResetForm();
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission) =>
        Roles.All.Any(role => user.IsInRole(role) && Permissions.Allows(role, permission));

    private async Task LoadLevel(int index, int parent)
    {
        IReadOnlyList<MasterDataEntity> loaded = index switch
        {
            0 => (await MasterData.ListChildrenAsync<Area>(0))
                .Where(x => x.IsActive)
                .Cast<MasterDataEntity>()
                .ToArray(),
            1 => (await MasterData.ListChildrenAsync<Product>(parent))
                .Where(x => x.IsActive)
                .Cast<MasterDataEntity>()
                .ToArray(),
            2 => (await MasterData.ListChildrenAsync<Line>(parent))
                .Where(x => x.IsActive)
                .Cast<MasterDataEntity>()
                .ToArray(),
            3 => (await MasterData.ListChildrenAsync<Station>(parent))
                .Where(x => x.IsActive)
                .Cast<MasterDataEntity>()
                .ToArray(),
            4 => (await MasterData.ListChildrenAsync<Tooling>(parent))
                .Where(x => x.IsActive)
                .Cast<MasterDataEntity>()
                .ToArray(),
            _ => (await MasterData.ListChildrenAsync<Characteristic>(parent))
                .Where(x => x.IsActive)
                .Cast<MasterDataEntity>()
                .ToArray(),
        };
        items[index].AddRange(loaded);
    }

    private async Task SelectAsync(int index, string? text)
    {
        selected[index] = int.TryParse(text, out var id)
            ? items[index].FirstOrDefault(x => x.Id == id)
            : null;
        for (var i = index + 1; i < 6; i++)
        {
            items[i].Clear();
            selected[i] = null;
        }
        error = null;
        if (selected[index] is not null && index < 5)
            await LoadLevel(index + 1, selected[index]!.Id);
        if (index == 5)
            await LoadRecentAsync();
    }

    private async Task LoadRecentAsync()
    {
        recent =
            selected[5] is Characteristic c && c.DataType == CharacteristicDataType.Continuous
                ? await CollectionService.ListRecentAsync(c.Id, 20)
                : [];
        var instrumentIds = recent
            .Where(x => x.InstrumentId.HasValue)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();
        instrumentNames = await Db
            .Instruments.Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code);
        var operatorIds = recent.Select(x => x.OperatorId).Distinct().ToArray();
        operatorNames = await UserManager
            .Users.Where(x => operatorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName);
        if (selected[5] is Characteristic characteristic)
        {
            values = new string[characteristic.SubgroupSize];
            valueRefs = new ElementReference[values.Length];
        }
    }

    private void ResetForm()
    {
        var now = LocalTime.ToLocal(DateTimeOffset.UtcNow).DateTime;
        collectedAt = now.AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);
        selectedShift = "T1";
        lot = "";
        note = "";
        instrumentId = null;
    }

    private async Task SaveAsync()
    {
        error = success = null;
        if (selected[5] is not Characteristic characteristic)
        {
            error = "Selecione uma característica.";
            return;
        }
        try
        {
            var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
            var input = new CollectionInput(
                characteristic.Id,
                values.Cast<object?>().ToArray(),
                instrumentId,
                LocalTime.ParseLocal(
                    collectedAt.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)
                ),
                selectedShift,
                lot.Trim(),
                note.Trim()
            );
            await CollectionService.SaveAsync(
                input,
                user.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
            );
            success = "Subgrupo registrado.";
            ResetForm();
            await LoadRecentAsync();
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
        }
    }

    private async Task Advance(KeyboardEventArgs e, int index)
    {
        if (e.Key == "Enter" && index + 1 < valueRefs.Length)
            await valueRefs[index + 1].FocusAsync();
    }

    private void AskExclude(int id)
    {
        excludeId = id;
        excludeReason = "";
    }

    private void CancelExclude() => excludeId = null;

    private async Task ExcludeAsync()
    {
        if (excludeId is null)
            return;
        try
        {
            var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
            await CollectionService.ExcludeAsync(
                excludeId.Value,
                excludeReason,
                user.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
            );
            excludeId = null;
            success = "Subgrupo excluído.";
            await LoadRecentAsync();
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
        }
    }

    private static string Display(MasterDataEntity item) =>
        item switch
        {
            Area x => x.Name,
            Product x => x.Name,
            Line x => x.Name,
            Station x => x.Name,
            Tooling x => $"{x.Code} · {x.Name}",
            Characteristic x => x.Name,
            _ => item.Id.ToString(),
        };

    private static string Number(double? value) =>
        value?.ToString("G", CultureInfo.GetCultureInfo("pt-BR")) ?? "—";

    // Display only: round to 4 decimals so floating-point noise (10,000000000000002) never
    // reaches the screen. Calculations keep full precision.
    private static string NumberValue(double value) =>
        Math.Round(value, 4).ToString("0.####", CultureInfo.GetCultureInfo("pt-BR"));

    private static string Average(double[] values) =>
        values.Length == 0 ? "—" : NumberValue(values.Average());

    private static string Range(double[] values) =>
        values.Length == 0 ? "—" : NumberValue(values.Max() - values.Min());

    private string InstrumentName(int? id) =>
        id is null ? "—" : instrumentNames.GetValueOrDefault(id.Value, "—");

    private string TemplateUrl(string extension, int id) =>
        $"/coleta/template.{extension}?characteristicId={id}";
}
