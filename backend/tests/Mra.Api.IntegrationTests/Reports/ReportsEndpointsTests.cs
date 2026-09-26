using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mra.Api.Authorization;
using Mra.Api.Endpoints;
using Mra.Application;
using Xunit;

namespace Mra.Api.IntegrationTests.Reports;

// FR-6.1 / A-7: the report endpoint carries the ApproverOrTenantAdmin policy. Which roles that
// policy admits (Requester -> 403) is covered by AuthorizationPolicyTests.
public sealed class ReportsEndpointsTests
{
    [Fact]
    public async Task Spend_endpoint_requires_the_ApproverOrTenantAdmin_policy()
    {
        var builder = WebApplication.CreateBuilder();
        // ISender must be a registered service, or minimal APIs would infer it as the body.
        builder.Services.AddApplication();
        builder.Services.AddAuthorizationPolicies();
        await using var app = builder.Build();

        app.MapReportsEndpoints();

        var endpoint = Assert.Single(
            ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>(),
            e => e.RoutePattern.RawText == "/api/reports/spend");
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy);
        Assert.Equal([Policies.ApproverOrTenantAdmin], policies);
        Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAllowAnonymous>());
    }
}
