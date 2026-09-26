namespace Mra.Domain.Users;

// Byte-backed so it maps to tinyint (architecture §6).
public enum Role : byte
{
    SystemAdmin = 1,
    TenantAdmin = 2,
    Requester = 3,
    Approver = 4,
}
