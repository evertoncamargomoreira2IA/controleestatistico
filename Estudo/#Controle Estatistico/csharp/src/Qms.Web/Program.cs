using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Domain.Security;
using Qms.Infrastructure;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;
using Qms.Web.Components;
using Qms.Web.Components.Account;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<
    AuthenticationStateProvider,
    IdentityRevalidatingAuthenticationStateProvider
>();
builder.Services.AddQmsInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder
    .Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue(
            "Security:MaxFailedAccessAttempts",
            5
        );
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue("Security:LockoutMinutes", 15)
        );
        options.Lockout.AllowedForNewUsers = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<QmsDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<
    IUserClaimsPrincipalFactory<ApplicationUser>,
    QmsClaimsPrincipalFactory
>();

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnValidatePrincipal = async context =>
    {
        var manager = context.HttpContext.RequestServices.GetRequiredService<
            UserManager<ApplicationUser>
        >();
        var user = await manager.GetUserAsync(context.Principal!);
        if (user is null || !user.IsActive)
            context.RejectPrincipal();
    };
});

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
        options.AddPolicy(
            $"perm:{permission}",
            policy =>
                policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        Roles.All.Any(role =>
                            context.User.IsInRole(role) && Permissions.Allows(role, permission)
                        )
                    )
        );
});
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.UseMigrationsEndPoint();
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
if (
    string.Equals(
        app.Configuration["Database:Provider"] ?? "Sqlite",
        "Sqlite",
        StringComparison.OrdinalIgnoreCase
    )
)
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<QmsDbContext>().Database.MigrateAsync();
}
await DbSeeder.SeedAsync(app.Services, app.Environment);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.Use(
    async (context, next) =>
    {
        var path = context.Request.Path;
        var exempt =
            path.StartsWithSegments("/Account/Manage/ChangePassword")
            || path.StartsWithSegments("/Account/Logout")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_blazor")
            || path.StartsWithSegments("/lib")
            || Path.HasExtension(path.Value);
        if (context.User.Identity?.IsAuthenticated == true && !exempt)
        {
            var userManager = context.RequestServices.GetRequiredService<
                UserManager<ApplicationUser>
            >();
            var user = await userManager.GetUserAsync(context.User);
            if (user?.MustChangePassword == true)
            {
                context.Response.Redirect("/Account/Manage/ChangePassword");
                return;
            }
        }
        await next();
    }
);
app.UseAuthorization();
app.UseAntiforgery();
app.MapGet("/Account/Register", () => Results.NotFound());
app.MapGet(
        "/coleta/template.{extension}",
        async (
            string extension,
            int characteristicId,
            QmsDbContext db,
            ITemplateService templates
        ) =>
        {
            if (extension is not ("xlsx" or "csv"))
                return Results.NotFound();
            var characteristic = await db.Characteristics.FindAsync(characteristicId);
            if (characteristic is null || !characteristic.IsActive)
                return Results.NotFound();
            var tooling = await db.Toolings.FindAsync(characteristic.ToolingId);
            if (tooling is null)
                return Results.NotFound();
            var safe = System
                .Text.RegularExpressions.Regex.Replace(
                    $"{tooling.Code}_{characteristic.Name}",
                    "[^A-Za-z0-9_-]+",
                    "_"
                )
                .Trim('_');
            var filename = $"coleta_{safe}.{extension}";
            var bytes =
                extension == "xlsx"
                    ? templates.BuildXlsx(characteristic)
                    : templates.BuildCsv(characteristic);
            var contentType =
                extension == "xlsx"
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : "text/csv; charset=utf-8";
            return Results.File(bytes, contentType, filename);
        }
    )
    .RequireAuthorization("perm:view");
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();
app.Run();
