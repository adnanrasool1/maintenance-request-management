using Mra.Domain.Sites;

namespace Mra.Application.Sites;

/// <summary>A site (contract §3.3, §3.5). Never carries the organisation ID.</summary>
public sealed record SiteDto(Guid Id, string Name);

public static class SiteMappings
{
    public static SiteDto ToDto(this Site site) => new(site.Id, site.Name);
}
