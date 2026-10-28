using MS.Cobroup.BackendAdmin.Domain.MachineAuth;

namespace MS.Cobroup.BackendAdmin.Core.Tests.MachineAuth;

public sealed class MachineAuthorizationRequestTests
{
    [Fact]
    public void Cancel_invalidates_an_unexpired_request()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new MachineAuthorizationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "machine-1", "hash", now, now.AddMinutes(120));

        request.Cancel(Guid.NewGuid(), now);

        Assert.False(request.IsValid(now.AddMinutes(1)));
        Assert.True(request.IsCancelled);
    }

    [Fact]
    public void Approval_and_cancellation_are_not_valid_requests()
    {
        var now = DateTimeOffset.UtcNow;
        var approved = new MachineAuthorizationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "machine-1", "hash", now, now.AddMinutes(120));
        var cancelled = new MachineAuthorizationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "machine-2", "hash", now, now.AddMinutes(120));

        approved.Approve(Guid.NewGuid(), now);
        cancelled.Cancel(Guid.NewGuid(), now);

        Assert.False(approved.IsValid(now));
        Assert.False(cancelled.IsValid(now));
    }
}
