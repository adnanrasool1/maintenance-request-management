using Microsoft.EntityFrameworkCore;
using Mra.Domain.Users;
using Xunit;

namespace Mra.Api.IntegrationTests.Persistence;

// Architecture §7 layer 3: the global query filters.
public sealed class TenantQueryFilterTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Org_user_sees_only_their_own_organisation_sites_and_users()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();

        await using var db = fixture.CreateContext(orgA.OrganisationId);

        Assert.Equal([orgA.OrganisationId], await db.Organisations.Select(o => o.Id).ToListAsync(Ct));
        Assert.Equal([orgA.SiteId], await db.Sites.Select(s => s.Id).ToListAsync(Ct));
        Assert.Equal([orgA.UserId], await db.Users.Select(u => u.Id).ToListAsync(Ct));
        Assert.DoesNotContain(orgB.SiteId, await db.Sites.Select(s => s.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task Org_user_cannot_load_another_organisations_rows_by_id()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        var requestB = await fixture.InsertRequestWithAuditAsync(orgB);

        await using var db = fixture.CreateContext(orgA.OrganisationId);

        Assert.Null(await db.Organisations.FindAsync([orgB.OrganisationId], Ct));
        Assert.Null(await db.Sites.FindAsync([orgB.SiteId], Ct));
        Assert.Null(await db.Users.FindAsync([orgB.UserId], Ct));
        Assert.Null(await db.MaintenanceRequests.FindAsync([requestB], Ct));

        Assert.Null(await db.Sites.FirstOrDefaultAsync(s => s.Id == orgB.SiteId, Ct));
        Assert.Null(await db.Users.FirstOrDefaultAsync(u => u.Id == orgB.UserId, Ct));
        Assert.Null(await db.MaintenanceRequests.FirstOrDefaultAsync(r => r.Id == requestB, Ct));
        Assert.False(await db.AuditEntries.AnyAsync(a => a.RequestId == requestB, Ct));
    }

    [Fact]
    public async Task Org_user_sees_only_their_own_requests_and_audit_entries()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        var requestA = await fixture.InsertRequestWithAuditAsync(orgA);
        await fixture.InsertRequestWithAuditAsync(orgB);

        await using var db = fixture.CreateContext(orgA.OrganisationId);

        Assert.Equal([requestA], await db.MaintenanceRequests.Select(r => r.Id).ToListAsync(Ct));
        Assert.Equal([requestA], await db.AuditEntries.Select(a => a.RequestId).ToListAsync(Ct));
    }

    [Fact]
    public async Task System_admin_with_no_organisation_sees_no_tenant_rows()
    {
        var tenant = await fixture.SeedTenantAsync();
        await fixture.InsertRequestWithAuditAsync(tenant);
        var systemAdmin = User.CreateSystemAdmin(SqlServerFixture.UniqueEmail(), "hash");
        await using (var seed = fixture.CreateContext(organisationId: null))
        {
            seed.Users.Add(systemAdmin);
            await seed.SaveChangesAsync(Ct);
        }

        await using var db = fixture.CreateContext(organisationId: null);

        Assert.False(await db.Organisations.AnyAsync(Ct));
        Assert.False(await db.Sites.AnyAsync(Ct));
        Assert.False(await db.MaintenanceRequests.AnyAsync(Ct));
        Assert.False(await db.AuditEntries.AnyAsync(Ct));

        // Neither tenant users nor the System Admin's own null-organisation row match "null == null".
        Assert.False(await db.Users.AnyAsync(Ct));
        Assert.Null(await db.Users.FindAsync([systemAdmin.Id], Ct));
    }
}
