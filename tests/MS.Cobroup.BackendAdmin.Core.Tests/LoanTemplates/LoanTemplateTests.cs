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
    public void UpdateCreatesImmutableHistoricalVersion()
    {
        var template = CreateTemplate();
        var original = Assert.Single(template.Versions);

        template.Update(
            "Credito flexible",
            null,
            LoanTemplateClassification.CustomTemplate,
            LoanTemplateStatus.Inactive,
            45,
            2_000_000m,
            [12.5m],
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-10-22T12:00:00Z"));

        Assert.Equal(2, template.Version);
        Assert.Equal(2, template.Versions.Count);
        Assert.Equal("Credito diario", original.Name);
        Assert.Equal(30, original.MaxLoanDays);
        Assert.Equal("Credito flexible", template.Name);
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
