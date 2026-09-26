namespace Mra.Domain.Organisations;

public sealed class Organisation
{
    private Organisation()
    {
        Name = null!;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public decimal ApprovalThreshold { get; private set; }

    public static Organisation Create(string name, decimal approvalThreshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(approvalThreshold);

        return new Organisation
        {
            Id = Guid.NewGuid(),
            Name = name,
            ApprovalThreshold = approvalThreshold,
        };
    }

    // Applies to requests raised from now on. Requests already pending or decided are not
    // re-evaluated (FR-4.3).
    public void SetApprovalThreshold(decimal approvalThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(approvalThreshold);

        ApprovalThreshold = approvalThreshold;
    }
}
