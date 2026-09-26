namespace Mra.Api.Authorization;

/// <summary>Authorization policy names (contract §1.1). Apply one to every endpoint or group.</summary>
public static class Policies
{
    public const string SystemAdmin = nameof(SystemAdmin);
    public const string TenantAdmin = nameof(TenantAdmin);

    /// <summary>Requester or Approver: an Approver can do everything a Requester can (contract D-1).</summary>
    public const string Requester = nameof(Requester);

    public const string Approver = nameof(Approver);
    public const string ApproverOrTenantAdmin = nameof(ApproverOrTenantAdmin);

    /// <summary>Any user with an organisation: TenantAdmin, Requester or Approver.</summary>
    public const string OrgMember = nameof(OrgMember);
}
