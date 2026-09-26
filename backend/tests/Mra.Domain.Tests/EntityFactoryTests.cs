using Mra.Domain.Organisations;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Xunit;

namespace Mra.Domain.Tests;

public sealed class EntityFactoryTests
{
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void Organisation_create_sets_fields_and_generates_id()
    {
        var org = Organisation.Create("Acme", 500m);

        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("Acme", org.Name);
        Assert.Equal(500m, org.ApprovalThreshold);
    }

    [Fact]
    public void Organisation_create_accepts_zero_threshold()
    {
        Assert.Equal(0m, Organisation.Create("Acme", 0m).ApprovalThreshold);
    }

    [Fact]
    public void Organisation_create_rejects_negative_threshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Organisation.Create("Acme", -0.01m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Organisation_create_rejects_blank_name(string name)
    {
        Assert.Throws<ArgumentException>(() => Organisation.Create(name, 100m));
    }

    [Fact]
    public void Site_create_rejects_empty_organisation_id()
    {
        Assert.Throws<ArgumentException>(() => Site.Create(Guid.Empty, "HQ"));
    }

    [Fact]
    public void Site_create_rejects_blank_name()
    {
        Assert.Throws<ArgumentException>(() => Site.Create(OrgId, " "));
    }

    [Fact]
    public void User_create_sets_organisation_and_role()
    {
        var user = User.Create(OrgId, "a@example.com", "hash", Role.Approver);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(OrgId, user.OrganisationId);
        Assert.Equal(Role.Approver, user.Role);
    }

    [Fact]
    public void User_create_rejects_system_admin_role()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => User.Create(OrgId, "a@example.com", "hash", Role.SystemAdmin));
    }

    [Fact]
    public void User_create_rejects_empty_organisation_id()
    {
        Assert.Throws<ArgumentException>(() => User.Create(Guid.Empty, "a@example.com", "hash", Role.Requester));
    }

    [Fact]
    public void User_create_rejects_blank_email_or_hash()
    {
        Assert.Throws<ArgumentException>(() => User.Create(OrgId, "", "hash", Role.Requester));
        Assert.Throws<ArgumentException>(() => User.Create(OrgId, "a@example.com", " ", Role.Requester));
    }

    [Fact]
    public void System_admin_has_no_organisation()
    {
        var admin = User.CreateSystemAdmin("admin@example.com", "hash");

        Assert.Null(admin.OrganisationId);
        Assert.Equal(Role.SystemAdmin, admin.Role);
    }
}
