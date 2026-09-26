using Mra.Api.IntegrationTests.Persistence;
using Mra.Application.Reports.SpendBySite;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Xunit;

namespace Mra.Api.IntegrationTests.Reports;

// Architecture §13: spend report totals against a known dataset in real SQL Server
// (range edges, status filter, zero-spend sites, other tenant excluded, grand total).
public sealed class SpendReportTests(SqlServerFixture fixture)
{
    private const decimal Threshold = 500m;

    // Report range 2026-09-01 .. 2026-09-30 (inclusive UTC days).
    private const string From = "2026-09-01";
    private const string To = "2026-09-30";
    private static readonly DateTime RangeStart = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayAfterRange = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RaisedAt = new(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Report_sums_completed_spend_per_site_within_the_inclusive_utc_range()
    {
        var orgA = await SeedOrganisationAsync("Charlie yard", "Alpha office", "Bravo depot");
        var orgB = await SeedOrganisationAsync("Alpha office");
        var (charlie, alpha, bravo) = (orgA.Sites[0], orgA.Sites[1], orgA.Sites[2]);

        await SaveAsync(orgA.OrganisationId,
            // Included: completed exactly at from 00:00:00Z.
            Completed(orgA, alpha, 50m, at: RangeStart),
            // Included: completed at to 23:59:59.999Z.
            Completed(orgA, alpha, 70m, at: DayAfterRange.AddMilliseconds(-1)),
            // Excluded: completed at (to + 1 day) 00:00:00Z.
            Completed(orgA, alpha, 1000m, at: DayAfterRange),
            // Excluded: completed one millisecond before the range starts.
            Completed(orgA, alpha, 2000m, at: RangeStart.AddMilliseconds(-1)),
            // Included: manually approved, then completed mid-range.
            Completed(orgA, bravo, 650m, at: new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc), estimatedCost: 600m),
            // Excluded: approved but not completed.
            Raised(orgA, bravo, estimatedCost: 100m),
            // Excluded: pending approval.
            Raised(orgA, bravo, estimatedCost: 600m));

        // Excluded: another organisation's completed request in range.
        await SaveAsync(orgB.OrganisationId,
            Completed(orgB, orgB.Sites[0], 999m, at: new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc)));

        var report = await RunAsync(orgA.OrganisationId, From, To);

        Assert.Equal(new DateOnly(2026, 9, 1), report.From);
        Assert.Equal(new DateOnly(2026, 9, 30), report.To);
        Assert.Equal(
            [
                new SpendRowDto(alpha, "Alpha office", 120m),
                new SpendRowDto(bravo, "Bravo depot", 650m),
                new SpendRowDto(charlie, "Charlie yard", 0m),
            ],
            report.Rows);
        Assert.Equal(770m, report.GrandTotal);
        Assert.DoesNotContain(report.Rows, row => row.SiteId == orgB.Sites[0]);
    }

    [Fact]
    public async Task Single_day_range_covers_that_whole_utc_day_only()
    {
        var org = await SeedOrganisationAsync("Main");
        var site = org.Sites[0];

        await SaveAsync(org.OrganisationId,
            Completed(org, site, 10m, at: new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)),
            Completed(org, site, 20m, at: new DateTime(2026, 9, 15, 23, 59, 59, 999, DateTimeKind.Utc)),
            Completed(org, site, 40m, at: new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc)));

        var report = await RunAsync(org.OrganisationId, "2026-09-15", "2026-09-15");

        Assert.Equal([new SpendRowDto(site, "Main", 30m)], report.Rows);
        Assert.Equal(30m, report.GrandTotal);
    }

    [Fact]
    public async Task Organisation_with_no_sites_gets_no_rows_and_a_zero_total()
    {
        var org = await SeedOrganisationAsync();

        var report = await RunAsync(org.OrganisationId, From, To);

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.GrandTotal);
    }

    [Fact]
    public async Task Caller_with_no_organisation_sees_no_sites()
    {
        await SeedOrganisationAsync("Somewhere");

        var report = await RunAsync(organisationId: null, From, To);

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.GrandTotal);
    }

    private async Task<SpendReportDto> RunAsync(Guid? organisationId, string from, string to)
    {
        await using var db = fixture.CreateContext(organisationId);
        return await new GetSpendBySiteHandler(db).Handle(new GetSpendBySiteQuery(from, to), Ct);
    }

    private async Task<SeededOrganisation> SeedOrganisationAsync(params string[] siteNames)
    {
        var organisation = Organisation.Create($"Org {Guid.NewGuid():N}", Threshold);
        var sites = siteNames.Select(name => Site.Create(organisation.Id, name)).ToList();
        var requester = User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Requester);
        var approver = User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Approver);

        // Created the way the System Admin creates an organisation: with no caller organisation.
        await using var db = fixture.CreateContext(organisationId: null);
        db.Add(organisation);
        db.AddRange(sites);
        db.AddRange(requester, approver);
        await db.SaveChangesAsync(Ct);

        return new SeededOrganisation(organisation.Id, sites.Select(s => s.Id).ToList(), requester.Id, approver.Id);
    }

    private async Task SaveAsync(Guid organisationId, params MaintenanceRequest[] requests)
    {
        await using var db = fixture.CreateContext(organisationId);
        db.MaintenanceRequests.AddRange(requests);
        await db.SaveChangesAsync(Ct);
    }

    // Below the threshold it is auto-approved; at or above it goes to PendingApproval.
    private static MaintenanceRequest Raised(SeededOrganisation org, Guid siteId, decimal estimatedCost) =>
        MaintenanceRequest.Raise(org.OrganisationId, siteId, org.RequesterId, "Report test", estimatedCost, Threshold, RaisedAt);

    // Raised, approved (automatically or by the Approver) and completed at the given time.
    private static MaintenanceRequest Completed(
        SeededOrganisation org, Guid siteId, decimal actualCost, DateTime at, decimal estimatedCost = 100m)
    {
        var request = Raised(org, siteId, estimatedCost);
        if (request.Status == RequestStatus.PendingApproval)
        {
            request.Approve(org.ApproverId, Threshold, comment: null, RaisedAt.AddHours(1));
        }

        request.Complete(org.RequesterId, actualCost, at);
        return request;
    }

    private sealed record SeededOrganisation(Guid OrganisationId, List<Guid> Sites, Guid RequesterId, Guid ApproverId);
}
