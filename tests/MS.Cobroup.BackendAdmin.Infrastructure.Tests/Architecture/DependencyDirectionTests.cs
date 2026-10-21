using MS.Cobroup.BackendAdmin.Infrastructure;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Architecture;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        var references = typeof(AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("MS.Cobroup.BackendAdmin.Api", references);
    }
}
