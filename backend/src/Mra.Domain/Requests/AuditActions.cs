namespace Mra.Domain.Requests;

// The values written to AuditEntry.Action. Only MaintenanceRequest records them.
public static class AuditActions
{
    public const string Raised = "Raised";

    public const string AutoApproved = "AutoApproved";

    public const string RoutedForApproval = "RoutedForApproval";

    public const string Approved = "Approved";

    public const string Rejected = "Rejected";

    public const string Completed = "Completed";
}
