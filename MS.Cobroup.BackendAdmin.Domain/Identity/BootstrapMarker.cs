namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed class BootstrapMarker
{
    private BootstrapMarker()
    {
    }

    private BootstrapMarker(string id, DateTimeOffset completedAtUtc)
    {
        Id = id;
        CompletedAtUtc = completedAtUtc;
    }

    public string Id { get; private set; } = string.Empty;

    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static BootstrapMarker Complete(DateTimeOffset completedAtUtc) => new("platform", completedAtUtc);
}
