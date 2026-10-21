using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed class BootstrapIdentityService(IIdentityRepository repository, IPasswordCredentialService passwords)
{
    public async Task<BootstrapIdentityResult> ExecuteAsync(
        BootstrapIdentityCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var organization = Organization.Create(command.OrganizationCode, command.OrganizationName, now);
        var users = new[]
        {
            CreateUser(organization.Id, command.Administrator, UserRole.Admin, now),
            CreateUser(organization.Id, command.Supervisor, UserRole.Supervisor, now),
            CreateUser(organization.Id, command.Collector, UserRole.Collector, now)
        };
        if (users.Select(user => user.NormalizedUsername).Distinct(StringComparer.Ordinal).Count() != users.Length)
        {
            throw new ArgumentException("Bootstrap usernames must be unique within the organization.", nameof(command));
        }

        var audit = SecurityAuditRecord.BootstrapCompleted("platform-bootstrap", command.SourceIp, now);

        var created = await repository.TryBootstrapAsync(organization, users, audit, cancellationToken);
        return created
            ? new BootstrapIdentityResult(true, organization.Id, users.Select(user => user.Id).ToArray())
            : new BootstrapIdentityResult(false, null, []);

        ApplicationUser CreateUser(
            Guid organizationId,
            BootstrapUserCommand user,
            UserRole role,
            DateTimeOffset createdAtUtc) =>
            ApplicationUser.Create(
                organizationId,
                user.Username,
                user.DisplayName,
                passwords.Hash(user.Password),
                role,
                createdAtUtc);
    }
}

public sealed record BootstrapIdentityCommand(
    string OrganizationCode,
    string OrganizationName,
    BootstrapUserCommand Administrator,
    BootstrapUserCommand Supervisor,
    BootstrapUserCommand Collector,
    string SourceIp);

public sealed record BootstrapUserCommand(string Username, string DisplayName, string Password);

public sealed record BootstrapIdentityResult(bool Created, Guid? OrganizationId, IReadOnlyCollection<Guid> UserIds);
