namespace Mra.Domain.Sites;

public sealed class Site
{
    private Site()
    {
        Name = null!;
    }

    public Guid Id { get; private set; }

    public Guid OrganisationId { get; private set; }

    public string Name { get; private set; }

    public static Site Create(Guid organisationId, string name)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("An organisation ID is required.", nameof(organisationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Site
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = name,
        };
    }
}
