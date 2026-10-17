using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Qms.Domain.Auditing;
using Qms.Infrastructure.Persistence;

namespace Qms.Web.Components.Pages;

public partial class History
{
    private const int PageSize = 50;
    private List<AuditRow> rows = [];
    private int page;
    private string entity = "";
    private string action = "";
    private string actor = "";
    private DateTime? from;
    private DateTime? to;

    protected override Task OnInitializedAsync() => LoadAsync(0);

    private async Task LoadAsync(int requestedPage)
    {
        page = Math.Max(0, requestedPage);
        var query = Db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(log => log.Entity.Contains(entity));
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(log => log.Action.Contains(action));
        if (from.HasValue)
            query = query.Where(log => log.OccurredAt >= LocalDayStart(from.Value));
        if (to.HasValue)
            query = query.Where(log => log.OccurredAt < LocalDayStart(to.Value.AddDays(1)));
        if (!string.IsNullOrWhiteSpace(actor))
        {
            var matchingActorIds = Db
                .Users.AsNoTracking()
                .Where(user => user.FullName.Contains(actor) || user.UserName!.Contains(actor))
                .Select(user => user.Id);
            query = query.Where(log =>
                log.ActorId != null && matchingActorIds.Contains(log.ActorId)
            );
        }
        var logs = await query
            .OrderByDescending(log => log.OccurredAt)
            .Skip(page * PageSize)
            .Take(PageSize)
            .ToListAsync();
        var ids = logs.Select(log => log.ActorId).Where(id => id is not null).Distinct().ToArray();
        var names = await Db
            .Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName);
        rows = logs.Select(log => new AuditRow(
                log,
                log.ActorId is not null && names.TryGetValue(log.ActorId, out var name)
                    ? name
                    : "Sistema"
            ))
            .ToList();
    }

    private static DateTimeOffset LocalDayStart(DateTime value)
    {
        var local = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(local, SaoPauloZone()),
            TimeSpan.Zero
        );
    }

    private static TimeZoneInfo SaoPauloZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
    }

    private static string FormatJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "—";
        try
        {
            return JsonSerializer.Serialize(
                JsonDocument.Parse(json).RootElement,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return TimeZoneInfo.ConvertTime(value, SaoPauloZone()).ToString("dd/MM/yyyy HH:mm:ss");
    }

    private sealed record AuditRow(AuditLog Log, string ActorName);
}
