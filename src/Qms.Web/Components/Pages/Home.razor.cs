using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Collection;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;

namespace Qms.Web.Components.Pages;

public partial class Home
{
    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private QmsDbContext Db { get; set; } = default!;
    private readonly System.Globalization.CultureInfo ptBr =
        System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
    private int activeUsers;
    private int analysesCount;
    private int subgroupsToday;

    protected override async Task OnInitializedAsync()
    {
        analysesCount = await Db.CepAnalyses.CountAsync();
        activeUsers = await UserManager.Users.CountAsync(user => user.IsActive);
        var localNow = LocalTime.ToLocal(DateTimeOffset.UtcNow);
        var localStart = new DateTimeOffset(localNow.Date, localNow.Offset);
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = localStart.AddDays(1).ToUniversalTime();
        subgroupsToday = await Db.Subgroups.CountAsync(item =>
            !item.IsExcluded && item.CollectedAt >= utcStart && item.CollectedAt < utcEnd
        );
    }
}
