using Microsoft.AspNetCore.Authorization;
using Mra.Domain.Users;

namespace Mra.Api.Authorization;

public static class AuthorizationSetup
{
    // Values of the "role" claim (contract §1), taken from the Domain enum so they can't drift.
    private const string SystemAdmin = nameof(Role.SystemAdmin);
    private const string TenantAdmin = nameof(Role.TenantAdmin);
    private const string Requester = nameof(Role.Requester);
    private const string Approver = nameof(Role.Approver);

    /// <summary>
    /// Role policies from contract §1.1, plus a fallback policy that requires an authenticated
    /// user, so an endpoint without an explicit policy is never anonymous.
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.SystemAdmin, policy => RequireAnyRole(policy, SystemAdmin))
            .AddPolicy(Policies.TenantAdmin, policy => RequireAnyRole(policy, TenantAdmin))
            .AddPolicy(Policies.Requester, policy => RequireAnyRole(policy, Requester, Approver))
            .AddPolicy(Policies.Approver, policy => RequireAnyRole(policy, Approver))
            .AddPolicy(Policies.ApproverOrTenantAdmin, policy => RequireAnyRole(policy, Approver, TenantAdmin))
            .AddPolicy(Policies.OrgMember, policy => RequireAnyRole(policy, TenantAdmin, Requester, Approver));

        return services;
    }

    private static void RequireAnyRole(AuthorizationPolicyBuilder policy, params string[] roles) =>
        policy.RequireAuthenticatedUser().RequireRole(roles);
}
