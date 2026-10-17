using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.Users;
using Qms.Domain.Security;
using Qms.Infrastructure.Persistence;

namespace Qms.Infrastructure.Identity;

public sealed class UserAdminService(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    QmsDbContext db,
    IAuditService audit
) : IUserAdminService
{
    public async Task<IReadOnlyList<UserRecord>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user =>
                user.UserName!.Contains(term) || user.FullName.Contains(term)
            );
        }

        var result = new List<UserRecord>();
        foreach (
            var user in await query.OrderBy(user => user.FullName).ToListAsync(cancellationToken)
        )
        {
            var userRoles = await users.GetRolesAsync(user);
            result.Add(
                new UserRecord(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.FullName,
                    userRoles.FirstOrDefault() ?? Roles.Leitor,
                    user.IsActive,
                    user.LockoutEnd,
                    null
                )
            );
        }
        return result;
    }

    public async Task<UserOperation> CreateAsync(
        string actorId,
        UserChange change,
        string? ip = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!Roles.All.Contains(change.Role))
            return Failure("Perfil inválido.");
        if (!await roles.RoleExistsAsync(change.Role))
            return Failure("O perfil selecionado não existe.");
        if (string.IsNullOrWhiteSpace(change.InitialPassword))
            return Failure("Informe a senha inicial.");
        var user = new ApplicationUser
        {
            UserName = change.UserName.Trim(),
            FullName = change.FullName.Trim(),
            IsActive = true,
            MustChangePassword = true,
        };
        var create = await users.CreateAsync(user, change.InitialPassword);
        if (!create.Succeeded)
            return Failure(Error(create.Errors));
        var assigned = await users.AddToRoleAsync(user, change.Role);
        if (!assigned.Succeeded)
        {
            await users.DeleteAsync(user);
            return Failure(Error(assigned.Errors));
        }

        await audit.RecordAsync(
            actorId,
            "user",
            user.Id,
            "user.create",
            after: Snapshot(user, change.Role),
            ip: ip,
            origin: "web",
            cancellationToken: cancellationToken
        );
        return new UserOperation(true);
    }

    public async Task<UserOperation> UpdateAsync(
        string actorId,
        string userId,
        UserChange change,
        string? ip = null,
        CancellationToken cancellationToken = default
    )
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return Failure("Usuário não encontrado.");
        var oldRole = (await users.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Leitor;
        var before = Snapshot(user, oldRole);
        if (!Roles.All.Contains(change.Role))
            return Failure("Perfil inválido.");
        if (!await roles.RoleExistsAsync(change.Role))
            return Failure("O perfil selecionado não existe.");
        if (actorId == user.Id && (!change.IsActive || change.Role != oldRole))
            return Failure("Não é permitido desativar ou rebaixar o próprio usuário.");
        if (
            oldRole == Roles.Admin
            && (change.Role != Roles.Admin || !change.IsActive)
            && user.IsActive
            && await ActiveAdminCountAsync() <= 1
        )
            return Failure("Não é possível remover ou rebaixar o último administrador ativo.");
        if (change.ResetPassword)
        {
            if (string.IsNullOrWhiteSpace(change.NewPassword))
                return Failure("Informe a nova senha.");
            var passwordErrors = new List<IdentityError>();
            foreach (var validator in users.PasswordValidators)
            {
                var validation = await validator.ValidateAsync(users, user, change.NewPassword);
                if (!validation.Succeeded)
                    passwordErrors.AddRange(validation.Errors);
            }
            if (passwordErrors.Count > 0)
                return Failure(Error(passwordErrors));
        }

        user.FullName = change.FullName.Trim();
        user.IsActive = change.IsActive;
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded)
            return Failure(Error(update.Errors));
        if (oldRole != change.Role)
        {
            var removed = await users.RemoveFromRoleAsync(user, oldRole);
            var added = removed.Succeeded ? await users.AddToRoleAsync(user, change.Role) : removed;
            if (!added.Succeeded)
                return Failure(Error(added.Errors));
        }

        if (change.ResetPassword)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var reset = await users.ResetPasswordAsync(user, token, change.NewPassword ?? "");
            if (!reset.Succeeded)
                return Failure(Error(reset.Errors));
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            user.MustChangePassword = true;
            var resetUpdate = await users.UpdateAsync(user);
            if (!resetUpdate.Succeeded)
                return Failure(Error(resetUpdate.Errors));
        }

        var newRole = change.Role;
        await audit.RecordAsync(
            actorId,
            "user",
            user.Id,
            "user.update",
            before,
            Snapshot(user, newRole),
            ip,
            "web",
            cancellationToken
        );
        if (change.ResetPassword)
            await audit.RecordAsync(
                actorId,
                "user",
                user.Id,
                "user.password_reset",
                before: before,
                after: Snapshot(user, newRole),
                ip: ip,
                origin: "web",
                cancellationToken: cancellationToken
            );
        return new UserOperation(true);
    }

    private Task<int> ActiveAdminCountAsync() =>
        (
            from user in db.Users
            where user.IsActive
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == Roles.Admin
            select user.Id
        )
            .Distinct()
            .CountAsync();

    private static object Snapshot(ApplicationUser user, string role) =>
        new
        {
            user.UserName,
            user.FullName,
            role,
            user.IsActive,
            user.LockoutEnd,
            user.MustChangePassword,
        };

    private static UserOperation Failure(string error) => new(false, error);

    private static string Error(IEnumerable<IdentityError> errors) =>
        string.Join(
            " ",
            errors.Select(error =>
                error.Code switch
                {
                    "DuplicateUserName" => "Este usuário já está cadastrado.",
                    "PasswordTooShort" => "A senha deve ter pelo menos 12 caracteres.",
                    "PasswordRequiresUpper" => "A senha deve conter uma letra maiúscula.",
                    "PasswordRequiresLower" => "A senha deve conter uma letra minúscula.",
                    "PasswordRequiresDigit" => "A senha deve conter um número.",
                    _ => error.Description,
                }
            )
        );
}
