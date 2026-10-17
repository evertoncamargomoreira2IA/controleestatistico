using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Qms.Application.MasterData;
using Qms.Domain.MasterData;
using Qms.Domain.Security;
using Qms.Web.Components.Ui;

namespace Qms.Web.Components.Pages;

public partial class MasterData
{
    private readonly Column[] columns =
    [
        new("Áreas", "Nova área"),
        new("Produtos", "Novo produto"),
        new("Linhas", "Nova linha"),
        new("Postos", "Novo posto"),
        new("Tooling", "Novo tooling"),
        new("Características", "Nova característica"),
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
    private readonly string[] newName = ["", "", "", "", "", ""];
    private string newCode = "";
    private string newDescription = "";
    private readonly string[] entityNames =
    [
        "areas",
        "products",
        "lines",
        "stations",
        "toolings",
        "characteristics",
    ];
    private bool canManage;
    private IReadOnlyList<string> breadcrumb = [];
    private string? errorMessage,
        successMessage,
        formError;
    private bool editing;
    private int editIndex;
    private double? oldLsl,
        oldNominal,
        oldUsl;
    private int oldSubgroupSize;
    private ChartType oldChartType;
    private bool CanManage => canManage;
    private string BreadcrumbText =>
        breadcrumb.Count == 0 ? "Gestão" : $"Gestão · {string.Join(" / ", breadcrumb)}";

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        canManage = Roles.All.Any(role =>
            user.IsInRole(role) && Permissions.Allows(role, "masterdata.manage")
        );
        await LoadAsync(0, 0);
    }

    private async Task LoadAsync(int index, int parentId)
    {
        if (index == 0)
            items[0].AddRange(await Service.ListChildrenAsync<Area>(0));
        else
            switch (index)
            {
                case 1:
                    items[1].AddRange(await Service.ListChildrenAsync<Product>(parentId));
                    break;
                case 2:
                    items[2].AddRange(await Service.ListChildrenAsync<Line>(parentId));
                    break;
                case 3:
                    items[3].AddRange(await Service.ListChildrenAsync<Station>(parentId));
                    break;
                case 4:
                    items[4].AddRange(await Service.ListChildrenAsync<Tooling>(parentId));
                    break;
                case 5:
                    items[5].AddRange(await Service.ListChildrenAsync<Characteristic>(parentId));
                    break;
            }
    }

    private async Task SelectAsync(int index, MasterDataEntity item)
    {
        selected[index] = item;
        for (var i = index + 1; i < 6; i++)
        {
            items[i].Clear();
            selected[i] = null;
        }
        editing = false;
        if (index < 5)
            await LoadAsync(index + 1, item.Id);
        breadcrumb = await Service.GetBreadcrumbAsync(entityNames[index], item.Id);
    }

    private async Task CreateAsync(int index)
    {
        errorMessage = null;
        var name = newName[index].Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Informe um nome.";
            return;
        }
        MasterDataEntity entity = index switch
        {
            0 => new Area { Name = name },
            1 => new Product { AreaId = selected[0]!.Id, Name = name },
            2 => new Line { ProductId = selected[1]!.Id, Name = name },
            3 => new Station { LineId = selected[2]!.Id, Name = name },
            4 => new Tooling
            {
                StationId = selected[3]!.Id,
                Code = newCode.Trim(),
                Name = name,
                Description = newDescription,
            },
            _ => new Characteristic { ToolingId = selected[4]!.Id, Name = name },
        };
        var actor = (
            await AuthenticationStateProvider.GetAuthenticationStateAsync()
        ).User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await CreateEntity(entity, actor);
        if (!result)
        {
            errorMessage = formError;
            return;
        }
        newName[index] = "";
        newCode = "";
        newDescription = "";
        items[index].Clear();
        await LoadAsync(index, index == 0 ? 0 : selected[index - 1]!.Id);
        successMessage = "Cadastro criado.";
    }

    private async Task<bool> CreateEntity(MasterDataEntity entity, string? actor)
    {
        return entity switch
        {
            Area x => await CreateTyped(x, actor),
            Product x => await CreateTyped(x, actor),
            Line x => await CreateTyped(x, actor),
            Station x => await CreateTyped(x, actor),
            Tooling x => await CreateTyped(x, actor),
            Characteristic x => await CreateTyped(x, actor),
            _ => false,
        };
    }

    private async Task<bool> CreateTyped<T>(T entity, string? actor)
        where T : MasterDataEntity
    {
        var result = await Service.CreateAsync(entity, actor);
        formError = result.Error;
        return result.Succeeded;
    }

    private void Edit(MasterDataEntity entity, int index)
    {
        selected[index] = entity;
        editIndex = index;
        editing = true;
        formError = null;
        if (entity is Characteristic c)
        {
            oldLsl = c.Lsl;
            oldNominal = c.Nominal;
            oldUsl = c.Usl;
            oldSubgroupSize = c.SubgroupSize;
            oldChartType = c.ChartType;
        }
    }

    private Task Close()
    {
        editing = false;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        var actor = (
            await AuthenticationStateProvider.GetAuthenticationStateAsync()
        ).User.FindFirstValue(ClaimTypes.NameIdentifier);
        var entity = selected[editIndex]!;
        if (entity is Characteristic c)
        {
            var lsl = c.Lsl;
            var nominal = c.Nominal;
            var usl = c.Usl;
            var size = c.SubgroupSize;
            var chart = c.ChartType;
            c.UpdateSpecification(oldLsl, oldNominal, oldUsl, oldSubgroupSize, oldChartType);
            c.UpdateSpecification(lsl, nominal, usl, size, chart);
        }
        formError = entity switch
        {
            Area x => (await Service.UpdateAsync(x, actor)).Error,
            Product x => (await Service.UpdateAsync(x, actor)).Error,
            Line x => (await Service.UpdateAsync(x, actor)).Error,
            Station x => (await Service.UpdateAsync(x, actor)).Error,
            Tooling x => (await Service.UpdateAsync(x, actor)).Error,
            Characteristic x => (await Service.UpdateAsync(x, actor)).Error,
            _ => null,
        };
        if (formError is null)
        {
            editing = false;
            successMessage = "Cadastro atualizado.";
            items[editIndex].Clear();
            await LoadAsync(editIndex, editIndex == 0 ? 0 : selected[editIndex - 1]!.Id);
        }
    }

    private static string Display(MasterDataEntity entity) =>
        entity switch
        {
            Area x => x.Name,
            Product x => x.Name,
            Line x => x.Name,
            Station x => x.Name,
            Tooling x => $"{x.Code} · {x.Name}",
            Characteristic x => $"{x.Name} · rev. {x.Revision} · {Specification(x)}",
            _ => entity.Id.ToString(),
        };

    private static string Specification(Characteristic x) =>
        $"LSL {x.Lsl?.ToString("G", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? "—"} · Nominal {x.Nominal?.ToString("G", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? "—"} · USL {x.Usl?.ToString("G", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? "—"}";

    private sealed record Column(string Title, string NewLabel);
}
