using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mra.Domain.Users;
using Xunit;

namespace Mra.Api.IntegrationTests.Persistence;

// Architecture §6 and §7 layer 6: constraints the database enforces even if the application is bypassed.
public sealed class SchemaConstraintTests(SqlServerFixture fixture)
{
    private const int ConstraintViolation = 547;
    private const int UniqueIndexViolation = 2601;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Composite_fk_rejects_a_request_whose_site_belongs_to_another_organisation()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();

        // The request's OrganisationId is the caller's, so the write guard passes; only the
        // database can see that the site belongs to org B.
        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => fixture.RaiseRequestAsync(orgA, siteId: orgB.SiteId));

        var sqlError = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(ConstraintViolation, sqlError.Number);
        Assert.Contains("FK_MaintenanceRequests_Sites_SiteId_OrganisationId", sqlError.Message);
    }

    [Fact]
    public async Task Deleting_a_request_does_not_cascade_to_its_audit_entries()
    {
        var tenant = await fixture.SeedTenantAsync();
        var requestId = await fixture.RaiseRequestAsync(tenant);

        await using var db = fixture.CreateContext(tenant.OrganisationId);
        var error = await Assert.ThrowsAsync<SqlException>(
            () => db.Database.ExecuteSqlAsync($"DELETE FROM MaintenanceRequests WHERE Id = {requestId}", Ct));

        Assert.Equal(ConstraintViolation, error.Number);
        Assert.Contains("FK_AuditEntries_MaintenanceRequests_RequestId", error.Message);
        Assert.True(await db.AuditEntries.AnyAsync(a => a.RequestId == requestId, Ct));
    }

    [Fact]
    public async Task Duplicate_email_violates_UX_Users_Email_across_organisations()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        var email = SqlServerFixture.UniqueEmail();

        await using var db = fixture.CreateContext(organisationId: null);
        db.Users.Add(User.Create(orgA.OrganisationId, email, "hash", Role.Approver));
        await db.SaveChangesAsync(Ct);
        db.Users.Add(User.Create(orgB.OrganisationId, email.ToUpperInvariant(), "hash", Role.Approver));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var sqlError = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(UniqueIndexViolation, sqlError.Number);
        Assert.Contains("UX_Users_Email", sqlError.Message);
    }

    [Fact]
    public async Task Check_constraint_rejects_an_undefined_role()
    {
        var tenant = await fixture.SeedTenantAsync();

        await using var db = fixture.CreateContext(organisationId: null);
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO Users (Id, OrganisationId, Email, PasswordHash, Role) VALUES ({Guid.NewGuid()}, {tenant.OrganisationId}, {SqlServerFixture.UniqueEmail()}, {"hash"}, {(byte)9})",
            Ct));

        Assert.Equal(ConstraintViolation, error.Number);
        Assert.Contains("CK_Users_Role", error.Message);
    }

    [Fact]
    public async Task Timestamps_read_back_as_utc()
    {
        var tenant = await fixture.SeedTenantAsync();
        var requestId = await fixture.RaiseRequestAsync(tenant, complete: true);

        await using var db = fixture.CreateContext(tenant.OrganisationId);
        var request = await db.MaintenanceRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, Ct);
        var entries = await db.AuditEntries.AsNoTracking().Where(a => a.RequestId == requestId).ToListAsync(Ct);

        Assert.Equal(DateTimeKind.Utc, request.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, request.CompletedAt!.Value.Kind);
        Assert.Equal(new DateTime(2026, 9, 26, 17, 45, 0, DateTimeKind.Utc), request.CompletedAt.Value);
        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(DateTimeKind.Utc, e.OccurredAt.Kind));
    }
}
