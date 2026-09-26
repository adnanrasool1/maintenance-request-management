using Microsoft.EntityFrameworkCore;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Mra.Infrastructure.Persistence;
using Xunit;

namespace Mra.Api.IntegrationTests.Persistence;

// Architecture §7 layer 5 and §8: the SaveChanges write guard.
public sealed class TenantGuardInterceptorTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Added_row_with_no_organisation_is_stamped_with_the_callers()
    {
        var orgA = await fixture.SeedTenantAsync();
        var site = Site.Create(orgA.OrganisationId, "Warehouse");

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            db.Sites.Add(site);
            db.Entry(site).Property(s => s.OrganisationId).CurrentValue = Guid.Empty;
            await db.SaveChangesAsync(Ct);
        }

        await using var check = fixture.CreateContext(orgA.OrganisationId);
        var saved = await check.Sites.SingleAsync(s => s.Id == site.Id, Ct);
        Assert.Equal(orgA.OrganisationId, saved.OrganisationId);
    }

    [Fact]
    public async Task Adding_a_row_for_another_organisation_is_rejected()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        var site = Site.Create(orgB.OrganisationId, "Injected");

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            db.Sites.Add(site);
            await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
        }

        await using var check = fixture.CreateContext(orgB.OrganisationId);
        Assert.False(await check.Sites.AnyAsync(s => s.Id == site.Id, Ct));
    }

    [Fact]
    public async Task Tenant_caller_cannot_add_a_user_without_an_organisation()
    {
        var orgA = await fixture.SeedTenantAsync();

        await using var db = fixture.CreateContext(orgA.OrganisationId);
        db.Users.Add(User.CreateSystemAdmin(SqlServerFixture.UniqueEmail(), "hash"));

        await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Modifying_or_deleting_another_organisations_row_is_rejected()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        Site siteB;
        await using (var readB = fixture.CreateContext(orgB.OrganisationId))
        {
            siteB = await readB.Sites.AsNoTracking().SingleAsync(s => s.Id == orgB.SiteId, Ct);
        }

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            db.Sites.Attach(siteB);
            db.Entry(siteB).Property(s => s.Name).CurrentValue = "Renamed by org A";
            await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
        }

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            db.Sites.Remove(siteB);
            await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
        }

        await using var check = fixture.CreateContext(orgB.OrganisationId);
        Assert.Equal("Head office", (await check.Sites.SingleAsync(s => s.Id == orgB.SiteId, Ct)).Name);
    }

    [Fact]
    public async Task Tenant_caller_cannot_modify_another_organisation()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        await using var readB = fixture.CreateContext(orgB.OrganisationId);
        var organisationB = await readB.Organisations.AsNoTracking().SingleAsync(Ct);

        await using var db = fixture.CreateContext(orgA.OrganisationId);
        db.Organisations.Attach(organisationB);
        db.Entry(organisationB).Property(o => o.ApprovalThreshold).CurrentValue = 0m;

        await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Moving_a_row_to_another_organisation_is_rejected_for_any_caller(bool callerIsTenant)
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        // A user, because Site.OrganisationId is part of the alternate key and EF already refuses to change it.
        User userA;
        await using (var readA = fixture.CreateContext(orgA.OrganisationId))
        {
            userA = await readA.Users.AsNoTracking().SingleAsync(u => u.Id == orgA.UserId, Ct);
        }

        await using (var db = fixture.CreateContext(callerIsTenant ? orgA.OrganisationId : null))
        {
            db.Users.Attach(userA);
            db.Entry(userA).Property(u => u.OrganisationId).CurrentValue = orgB.OrganisationId;
            await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
        }

        await using var check = fixture.CreateContext(orgA.OrganisationId);
        Assert.True(await check.Users.AnyAsync(u => u.Id == orgA.UserId, Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Audit_entries_cannot_be_modified_or_deleted_by_any_caller(bool callerIsTenant)
    {
        var tenant = await fixture.SeedTenantAsync();
        var requestId = await fixture.InsertRequestWithAuditAsync(tenant);
        Mra.Domain.Requests.AuditEntry entry;
        await using (var read = fixture.CreateContext(tenant.OrganisationId))
        {
            entry = await read.AuditEntries.AsNoTracking().SingleAsync(a => a.RequestId == requestId, Ct);
        }

        Guid? caller = callerIsTenant ? tenant.OrganisationId : null;

        await using (var db = fixture.CreateContext(caller))
        {
            db.AuditEntries.Attach(entry);
            db.Entry(entry).Property(a => a.Comment).CurrentValue = "rewritten";
            await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
        }

        await using (var db = fixture.CreateContext(caller))
        {
            db.AuditEntries.Remove(entry);
            await Assert.ThrowsAsync<TenantGuardException>(() => db.SaveChangesAsync(Ct));
        }

        await using var check = fixture.CreateContext(tenant.OrganisationId);
        var stored = await check.AuditEntries.SingleAsync(a => a.RequestId == requestId, Ct);
        Assert.Null(stored.Comment);
    }
}
