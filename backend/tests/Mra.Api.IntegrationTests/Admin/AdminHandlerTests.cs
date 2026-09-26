using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mra.Api.IntegrationTests.Persistence;
using Mra.Application.Admin.CreateOrganisation;
using Mra.Application.Admin.CreateSite;
using Mra.Application.Admin.CreateUser;
using Mra.Application.Admin.SetThreshold;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Application.Sites.GetSites;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Xunit;

namespace Mra.Api.IntegrationTests.Admin;

// T6: the admin handlers and the site list against the real database, as each caller would run
// them (the context's query filters and the handler's ICurrentUser use the same organisation).
public sealed class AdminHandlerTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task System_admin_creates_organisation_and_tenant_admin_who_sees_only_their_own_sites()
    {
        var other = await fixture.SeedTenantAsync();
        var email = SqlServerFixture.UniqueEmail();

        CreateOrganisationResponse response;
        await using (var db = fixture.CreateContext(organisationId: null))
        {
            var handler = new CreateOrganisationHandler(db, new FakePasswordHasher());
            response = await handler.Handle(
                new CreateOrganisationCommand("  Acme Facilities  ", 5000.00m, $"  {email}  ", "a-long-password"), Ct);
        }

        Assert.Equal("Acme Facilities", response.Name);
        Assert.Equal(5000.00m, response.ApprovalThreshold);
        Assert.Equal(email, response.TenantAdmin.Email);
        Assert.Equal("TenantAdmin", response.TenantAdmin.Role);

        // The response has no organisation ID (D-3), so read it back the way only a test may.
        Guid organisationId;
        await using (var db = fixture.CreateContext(organisationId: null))
        {
            var admin = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == response.TenantAdmin.Id, Ct);
            Assert.Equal(Role.TenantAdmin, admin.Role);
            Assert.Equal("hashed:a-long-password", admin.PasswordHash);
            organisationId = admin.OrganisationId!.Value;
        }

        await using (var db = fixture.CreateContext(organisationId))
        {
            var sites = await new GetSitesHandler(db).Handle(new GetSitesQuery(), Ct);
            Assert.Empty(sites);
            Assert.DoesNotContain(other.OrganisationId, await db.Organisations.Select(o => o.Id).ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task Duplicate_email_in_another_organisation_is_a_validation_error_on_email_ignoring_case()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();
        var email = SqlServerFixture.UniqueEmail();

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            await CreateUserHandler(db, orgA).Handle(new CreateUserCommand(email.ToUpperInvariant(), "password1", "Requester"), Ct);
        }

        await using (var db = fixture.CreateContext(orgB.OrganisationId))
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(() =>
                CreateUserHandler(db, orgB).Handle(new CreateUserCommand(email, "password1", "approver"), Ct));

            var failure = Assert.Single(exception.Errors);
            Assert.Equal("Email", failure.PropertyName);
            Assert.Equal("Email is already in use.", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task Duplicate_admin_email_when_creating_an_organisation_is_a_validation_error_on_adminEmail()
    {
        var existing = await fixture.SeedTenantAsync();
        string existingEmail;
        await using (var db = fixture.CreateContext(existing.OrganisationId))
        {
            existingEmail = await db.Users.Where(u => u.Id == existing.UserId).Select(u => u.Email).SingleAsync(Ct);
        }

        await using (var db = fixture.CreateContext(organisationId: null))
        {
            var handler = new CreateOrganisationHandler(db, new FakePasswordHasher());
            var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
                new CreateOrganisationCommand($"Org {Guid.NewGuid():N}", 100m, existingEmail.ToUpperInvariant(), "a-long-password"), Ct));

            Assert.Equal("AdminEmail", Assert.Single(exception.Errors).PropertyName);
        }

        // The organisation was not saved either: one save for both rows.
        await using (var db = fixture.CreateContext(organisationId: null))
        {
            Assert.Equal(1, await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == existingEmail, Ct));
        }
    }

    [Fact]
    public async Task Created_user_belongs_to_the_callers_organisation_with_the_requested_role()
    {
        var tenant = await fixture.SeedTenantAsync();
        var email = SqlServerFixture.UniqueEmail();

        await using (var db = fixture.CreateContext(tenant.OrganisationId))
        {
            var dto = await CreateUserHandler(db, tenant).Handle(new CreateUserCommand($" {email} ", "password1", "approver"), Ct);
            Assert.Equal(email, dto.Email);
            Assert.Equal("Approver", dto.Role);
        }

        await using (var db = fixture.CreateContext(tenant.OrganisationId))
        {
            var user = await db.Users.SingleAsync(u => u.Email == email, Ct);
            Assert.Equal(tenant.OrganisationId, user.OrganisationId);
            Assert.Equal(Role.Approver, user.Role);
        }
    }

    [Fact]
    public async Task Duplicate_site_name_in_the_same_organisation_is_rejected_but_allowed_in_another()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            var site = await CreateSiteHandler(db, orgA).Handle(new CreateSiteCommand("  Depot  "), Ct);
            Assert.Equal("Depot", site.Name);
        }

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(() =>
                CreateSiteHandler(db, orgA).Handle(new CreateSiteCommand("DEPOT"), Ct));
            Assert.Equal("Name", Assert.Single(exception.Errors).PropertyName);
        }

        await using (var db = fixture.CreateContext(orgB.OrganisationId))
        {
            var site = await CreateSiteHandler(db, orgB).Handle(new CreateSiteCommand("Depot"), Ct);
            Assert.Equal("Depot", site.Name);
        }
    }

    [Fact]
    public async Task Unique_index_violations_are_translated_for_the_handlers()
    {
        // The race paths: the rows reach the database without the handlers' pre-check.
        var tenant = await fixture.SeedTenantAsync();

        await using (var db = fixture.CreateContext(tenant.OrganisationId))
        {
            db.Sites.Add(Site.Create(tenant.OrganisationId, "HEAD OFFICE"));
            await Assert.ThrowsAsync<DuplicateSiteNameException>(() => db.SaveChangesAsync(Ct));
        }

        await using (var db = fixture.CreateContext(tenant.OrganisationId))
        {
            var email = await db.Users.Where(u => u.Id == tenant.UserId).Select(u => u.Email).SingleAsync(Ct);
            db.Users.Add(User.Create(tenant.OrganisationId, email, "hash", Role.Approver));
            await Assert.ThrowsAsync<DuplicateEmailException>(() => db.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task Threshold_update_changes_only_the_callers_organisation()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            var response = await new SetThresholdHandler(db, new FakeCurrentUser(orgA.OrganisationId))
                .Handle(new SetThresholdCommand(750.25m), Ct);
            Assert.Equal(750.25m, response.ApprovalThreshold);
        }

        await using (var db = fixture.CreateContext(organisationId: null))
        {
            var thresholds = await db.Organisations.IgnoreQueryFilters()
                .Where(o => o.Id == orgA.OrganisationId || o.Id == orgB.OrganisationId)
                .ToDictionaryAsync(o => o.Id, o => o.ApprovalThreshold, Ct);

            Assert.Equal(750.25m, thresholds[orgA.OrganisationId]);
            Assert.Equal(500m, thresholds[orgB.OrganisationId]);
        }
    }

    [Fact]
    public async Task Get_sites_returns_only_the_callers_sites_ordered_by_name()
    {
        var orgA = await fixture.SeedTenantAsync();
        var orgB = await fixture.SeedTenantAsync();

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            db.Sites.AddRange(Site.Create(orgA.OrganisationId, "Zeta yard"), Site.Create(orgA.OrganisationId, "Alpha depot"));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = fixture.CreateContext(orgA.OrganisationId))
        {
            var sites = await new GetSitesHandler(db).Handle(new GetSitesQuery(), Ct);

            Assert.Equal(["Alpha depot", "Head office", "Zeta yard"], sites.Select(s => s.Name));
            Assert.Contains(sites, s => s.Id == orgA.SiteId);
            Assert.DoesNotContain(sites, s => s.Id == orgB.SiteId);
        }
    }

    private static CreateUserHandler CreateUserHandler(IAppDbContext db, Tenant tenant) =>
        new(db, new FakeCurrentUser(tenant.OrganisationId), new FakePasswordHasher());

    private static CreateSiteHandler CreateSiteHandler(IAppDbContext db, Tenant tenant) =>
        new(db, new FakeCurrentUser(tenant.OrganisationId));

    private sealed class FakeCurrentUser(Guid organisationId) : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();

        public Guid? OrganisationId => organisationId;

        public string? Role => "TenantAdmin";

        public bool IsAuthenticated => true;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string hash, string password) => hash == Hash(password);
    }
}
