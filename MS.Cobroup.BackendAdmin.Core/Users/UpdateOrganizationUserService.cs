using System.Text.Json;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Users;

public sealed class UpdateOrganizationUserService(IUserManagementRepository users, IUserEditingRepository repository, IPasswordCredentialService passwords)
{
    public async Task<UpdateOrganizationUserResult> ExecuteAsync(TenantActor actor, Guid userId, UpdateOrganizationUserInput input, CancellationToken cancellationToken)
    {
        if (actor.Role != UserRole.Admin || !await users.IsActiveAdminAsync(actor.OrganizationId, actor.UserId, cancellationToken))
            return new(UserUpdateOutcome.Forbidden);
        var membership = await repository.FindAsync(actor.OrganizationId, userId, cancellationToken);
        if (membership is null) return new(UserUpdateOutcome.NotFound);
        var user = membership.User;
        if (membership.Role == UserRole.Admin && userId != actor.UserId)
            return new(UserUpdateOutcome.Forbidden, Error: "Solo puedes modificar tu propio perfil administrativo.");
        if (!user.IsActive) return new(UserUpdateOutcome.Conflict, Error: "Esta cuenta está inhabilitada globalmente.");
        if (membership.Role == UserRole.Admin)
        {
            if (input.Role != UserRole.Admin || !input.IsActive)
                return new(UserUpdateOutcome.Invalid, Error: "El administrador debe conservar su rol y acceso activo.");
        }
        else if (input.Role is not (UserRole.Supervisor or UserRole.Collector))
            return new(UserUpdateOutcome.Invalid, Error: "Solo se permiten los roles Supervisor y Colaborador.");
        try { UserCredentialValidation.Validate(input.Username, input.DisplayName, input.Password is null or "" ? "unchanged" : input.Password, input.PhoneNumber); }
        catch (ArgumentException exception) { return new(UserUpdateOutcome.Invalid, Error: exception.Message); }

        var passwordChanged = !string.IsNullOrEmpty(input.Password);
        var profileChanged = user.Username != input.Username.Trim() || user.DisplayName != input.DisplayName.Trim()
            || user.PhoneNumber != input.PhoneNumber.Trim();
        if ((profileChanged || passwordChanged) && await repository.HasOtherMembershipsAsync(actor.OrganizationId, userId, cancellationToken))
            return new(UserUpdateOutcome.Conflict, Error: "La cuenta pertenece a varias organizaciones. Aquí solo puedes modificar su rol y acceso en esta organización.");
        var accessChanged = membership.Role != input.Role || membership.IsActive != input.IsActive;
        if ((membership.Role != input.Role || (membership.IsActive && !input.IsActive))
            && await repository.HasActiveRouteAssignmentsAsync(actor.OrganizationId, userId, cancellationToken))
            return new(UserUpdateOutcome.Conflict, Error: "Reasigna primero las rutas activas del usuario antes de cambiar su rol o estado.");

        var before = new { user.Username, user.DisplayName, user.PhoneNumber, membership.Role, membership.IsActive, membership.CanReopenDay };
        var now = DateTimeOffset.UtcNow;
        user.UpdateProfile(input.Username, input.DisplayName, input.PhoneNumber, passwordChanged ? passwords.Hash(input.Password!) : null, now);
        membership.UpdateAccess(input.Role, input.IsActive, input.CanReopenDay);
        var details = JsonSerializer.Serialize(new { before, after = new { user.Username, user.DisplayName, user.PhoneNumber, membership.Role, membership.IsActive, membership.CanReopenDay }, passwordChanged });
        var audit = AdministrativeAuditRecord.UserUpdated(actor.OrganizationId, actor.UserId, userId, now, details);
        var saved = await repository.SaveAsync(membership, audit, passwordChanged || before.Username != user.Username, accessChanged, cancellationToken);
        return saved
            ? new(UserUpdateOutcome.Success, new(user.Id, user.Username, user.DisplayName, membership.Role, membership.IsActive, membership.IsOwner, membership.JoinedAtUtc, user.PhoneNumber, membership.CanReopenDay))
            : new(UserUpdateOutcome.Conflict, Error: "El nombre de usuario no está disponible. Elige otro.");
    }
}

public sealed record UpdateOrganizationUserInput(string Username, string DisplayName, string PhoneNumber, UserRole Role, bool IsActive, string? Password = null, bool CanReopenDay = false);
public enum UserUpdateOutcome { Success, Forbidden, NotFound, Invalid, Conflict }
public sealed record UpdateOrganizationUserResult(UserUpdateOutcome Outcome, OrganizationUser? User = null, string? Error = null);
