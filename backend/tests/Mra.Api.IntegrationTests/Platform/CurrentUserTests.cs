using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mra.Infrastructure.Authentication;
using Xunit;

namespace Mra.Api.IntegrationTests.Platform;

public sealed class CurrentUserTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void Reads_sub_org_and_role_claims()
    {
        var user = CurrentUserFor(Authenticated(("sub", UserId.ToString()), ("org", OrgId.ToString()), ("role", "Requester")));

        Assert.True(user.IsAuthenticated);
        Assert.Equal(UserId, user.UserId);
        Assert.Equal(OrgId, user.OrganisationId);
        Assert.Equal("Requester", user.Role);
    }

    [Fact]
    public void Ignores_organisation_supplied_in_route_query_header_or_body()
    {
        var otherOrg = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext
        {
            User = Authenticated(("sub", UserId.ToString()), ("org", OrgId.ToString()), ("role", "Approver")),
        };
        context.Request.QueryString = new QueryString($"?org={otherOrg}&organisationId={otherOrg}");
        context.Request.RouteValues["org"] = otherOrg;
        context.Request.Headers["org"] = otherOrg;

        var user = new CurrentUser(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(OrgId, user.OrganisationId);
    }

    [Fact]
    public void System_admin_has_no_organisation()
    {
        var user = CurrentUserFor(Authenticated(("sub", UserId.ToString()), ("role", "SystemAdmin")));

        Assert.Equal(UserId, user.UserId);
        Assert.Null(user.OrganisationId);
        Assert.Equal("SystemAdmin", user.Role);
    }

    [Fact]
    public void Claims_on_an_unauthenticated_identity_are_not_trusted()
    {
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", UserId.ToString()), new Claim("org", OrgId.ToString()), new Claim("role", "Approver")]));

        var user = CurrentUserFor(unauthenticated);

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Null(user.OrganisationId);
        Assert.Null(user.Role);
    }

    [Fact]
    public void No_http_context_means_anonymous()
    {
        var user = new CurrentUser(new HttpContextAccessor());

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Null(user.OrganisationId);
    }

    [Fact]
    public void Malformed_guid_claim_reads_as_null()
    {
        var user = CurrentUserFor(Authenticated(("sub", "not-a-guid"), ("org", "also-not"), ("role", "Requester")));

        Assert.Null(user.UserId);
        Assert.Null(user.OrganisationId);
    }

    private static ClaimsPrincipal Authenticated(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), authenticationType: "Bearer"));

    private static CurrentUser CurrentUserFor(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
}
