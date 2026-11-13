using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;

namespace MS.Cobroup.BackendAdmin.Core.Tests.LoanTemplates;

public sealed class LoanTemplateTests
{
    [Fact]
    public void CreateNormalizesValuesAndRecordsInitialVersion()
    {
        var actorId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

        var template = LoanTemplate.Create(
            Guid.NewGuid(),
            "  Credito diario  ",
            "  Plantilla base  ",
            LoanTemplateClassification.Low,
            LoanTemplateStatus.Published,
            30,
            1_500_000m,
            [10m, 5m, 10m],
            actorId,
            createdAt);

        Assert.Equal("Credito diario", template.Name);
        Assert.Equal("CREDITO DIARIO", template.NormalizedName);
        Assert.Equal([5m, 10m], template.AllowedPercentages);
        Assert.Equal(1, template.Version);
        var version = Assert.Single(template.Versions);
        Assert.Equal(LoanTemplateStatus.Published, version.Status);
        Assert.Equal(actorId, version.RecordedByUserId);
    }

    [Fact]
    public void UpdateModifiesTemplateInPlaceWithoutCreatingNewVersion()
    {
        var template = CreateTemplate();
        var original = Assert.Single(template.Versions);

        var newRouteId = Guid.NewGuid();
        template.Update(
            "Credito flexible",
            null,
            LoanTemplateClassification.CustomTemplate,
            LoanTemplateStatus.Inactive,
            45,
            2_000_000m,
            [12.5m],
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-10-22T12:00:00Z"), minPrincipalAmount: 500m, routeIds: [newRouteId]);

        Assert.Equal(1, template.Version);
        Assert.Single(template.Versions);
        Assert.Equal("Credito flexible", template.Name);
        Assert.Equal(45, template.MaxLoanDays);
        Assert.Equal(500m, template.MinPrincipalAmount);
        Assert.Equal(2_000_000m, template.MaxPrincipalAmount);
        Assert.Equal(LoanTemplateStatus.Inactive, template.Status);
        Assert.Contains(newRouteId, template.RouteIds);
    }

    [Fact]
    public void SetRouteAssignmentAddsAndRemovesRoute()
    {
        var template = CreateTemplate();
        var actorId = Guid.NewGuid();
        var route1 = Guid.NewGuid();
        var route2 = Guid.NewGuid();

        Assert.True(template.SetRouteAssignment(route1, true, actorId, DateTimeOffset.UtcNow));
        Assert.Contains(route1, template.RouteIds);

        Assert.True(template.SetRouteAssignment(route2, true, actorId, DateTimeOffset.UtcNow));
        Assert.Equal(2, template.RouteIds.Count);

        Assert.True(template.SetRouteAssignment(route1, false, actorId, DateTimeOffset.UtcNow));
        Assert.DoesNotContain(route1, template.RouteIds);
        Assert.Contains(route2, template.RouteIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public void CreateRejectsInvalidMaximumDays(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTemplate(days));
    }

    [Fact]
    public void CreateRejectsEmptyPercentages()
    {
        Assert.Throws<ArgumentException>(() => LoanTemplate.Create(
            Guid.NewGuid(),
            "Credito diario",
            null,
            LoanTemplateClassification.Low,
            LoanTemplateStatus.Draft,
            30,
            1_000m,
            [],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CreateAllowsZeroPercentInterest()
    {
        var template = LoanTemplate.Create(
            Guid.NewGuid(),
            "Credito 0 porciento",
            null,
            LoanTemplateClassification.Low,
            LoanTemplateStatus.Published,
            30,
            1_000m,
            [0m, 5m],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Equal([0m, 5m], template.AllowedPercentages);
        Assert.Equal(0m, template.MinPercentage);
        Assert.Equal(5m, template.MaxPercentage);
    }

    [Fact]
    public void CreateRejectsNegativePercentages()
    {
        Assert.Throws<ArgumentException>(() => LoanTemplate.Create(
            Guid.NewGuid(),
            "Credito invalido",
            null,
            LoanTemplateClassification.Low,
            LoanTemplateStatus.Draft,
            30,
            1_000m,
            [-1m, 5m],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }

    private static LoanTemplate CreateTemplate(int days = 30) => LoanTemplate.Create(
        Guid.NewGuid(),
        "Credito diario",
        null,
        LoanTemplateClassification.Low,
        LoanTemplateStatus.Draft,
        days,
        1_000_000m,
        [5m, 10m],
        Guid.NewGuid(),
        DateTimeOffset.Parse("2026-10-21T12:00:00Z"));
}
