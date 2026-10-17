using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Qms.Application.Users;
using Qms.Domain.Security;

namespace Qms.Web.Components.Pages;

public partial class Users
{
    private IReadOnlyList<UserRecord> users = [];
    private string search = "";
    private string? message;
    private string? formError;
    private bool error;
    private bool editing;
    private UserRecord? selected;
    private UserForm model = new();

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync() => users = await UserAdmin.ListAsync(search);

    private void NewUser()
    {
        selected = null;
        model = new();
        formError = null;
        editing = true;
    }

    private void EditUser(UserRecord user)
    {
        selected = user;
        model = new()
        {
            UserName = user.UserName,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
        };
        formError = null;
        editing = true;
    }

    private void Close() => editing = false;

    private async Task SaveAsync()
    {
        var actor = (
            await AuthenticationStateProvider.GetAuthenticationStateAsync()
        ).User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(actor))
        {
            formError = "Não foi possível identificar o operador.";
            return;
        }
        var change = new UserChange(
            model.UserName,
            model.FullName,
            model.Role,
            model.Password,
            model.IsActive,
            model.ResetPassword,
            model.Password
        );
        var ip = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var result = selected is null
            ? await UserAdmin.CreateAsync(actor, change, ip)
            : await UserAdmin.UpdateAsync(actor, selected.Id, change, ip);
        if (!result.Succeeded)
        {
            formError = result.Error;
            return;
        }
        editing = false;
        message = "Usuário salvo com sucesso.";
        error = false;
        await LoadAsync();
    }

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private static string RoleName(string role) =>
        role switch
        {
            Roles.Admin => "Administrador",
            Roles.Engenheiro => "Engenheiro",
            Roles.Tecnico => "Técnico",
            Roles.Qualidade => "Qualidade",
            Roles.Leitor => "Leitor",
            _ => role,
        };

    private sealed class UserForm
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Informe o usuário.")]
        public string UserName { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Informe o nome completo.")]
        public string FullName { get; set; } = "";
        public string Role { get; set; } = Roles.Leitor;
        public string Password { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool ResetPassword { get; set; }
    }
}
