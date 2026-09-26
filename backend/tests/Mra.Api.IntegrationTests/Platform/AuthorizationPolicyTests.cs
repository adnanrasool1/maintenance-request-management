using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Mra.Api.Authorization;
using Xunit;

namespace Mra.Api.IntegrationTests.Platform;

// Policy matrix from contract §1.1, evaluated by the real authorization service.
public sealed class AuthorizationPolicyTests
{
    private static readonly string[] AllRoles = ["SystemAdmin", "TenantAdmin", "Requester", "Approver"];

    public static TheoryData<string, string[]> Matrix => new()
    {
        { Policies.SystemAdmin, ["SystemAdmin"] },
        { Policies.TenantAdmin, ["TenantAdmin"] },
        { Policies.Requester, ["Requester", "Approver"] },
        { Policies.Approver, ["Approver"] },
        { Policies.ApproverOrTenantAdmin, ["Approver", "TenantAdmin"] },
        { Policies.OrgMember, ["TenantAdmin", "Requester", "Approver"] },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Policy_admits_exactly_its_roles(string policy, string[] allowed)
    {
        var authorization = CreateAuthorizationService();

        foreach (var role in AllRoles)
        {
            var result = await authorization.AuthorizeAsync(Principal(role, authenticated: true), policy);
            Assert.True(result.Succeeded == allowed.Contains(role), $"{policy} / {role}: expected {allowed.Contains(role)}");
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Policy_rejects_an_unauthenticated_identity_even_with_a_role_claim(string policy, string[] allowed)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(Principal(allowed[0], authenticated: false), policy);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Fallback_policy_requires_an_authenticated_user()
    {
        var services = CreateServices();
        var fallback = await services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        var authorization = services.GetRequiredService<IAuthorizationService>();

        Assert.NotNull(fallback);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), fallback)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(Principal("Requester", authenticated: true), fallback)).Succeeded);
    }

    private static ClaimsPrincipal Principal(string role, bool authenticated) =>
        new(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            authenticationType: authenticated ? "Bearer" : null,
            nameType: "sub",
            roleType: "role"));

    private static ServiceProvider CreateServices() =>
        new ServiceCollection().AddLogging().AddAuthorizationPolicies().BuildServiceProvider();

    private static IAuthorizationService CreateAuthorizationService() =>
        CreateServices().GetRequiredService<IAuthorizationService>();
}
