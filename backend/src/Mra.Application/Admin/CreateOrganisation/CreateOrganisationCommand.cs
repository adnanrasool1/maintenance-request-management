using MediatR;

namespace Mra.Application.Admin.CreateOrganisation;

/// <summary>Creates an organisation and its first Tenant Admin (contract §3.1, FR-2.2).</summary>
public sealed record CreateOrganisationCommand(
    string Name,
    decimal? ApprovalThreshold,
    string AdminEmail,
    string AdminPassword) : IRequest<CreateOrganisationResponse>;

/// <summary>No organisation ID: organisation IDs never leave the server (contract D-3).</summary>
public sealed record CreateOrganisationResponse(string Name, decimal ApprovalThreshold, UserDto TenantAdmin);
