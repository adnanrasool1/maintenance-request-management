using Mra.Infrastructure.Authentication;
using Xunit;

namespace Mra.Api.IntegrationTests.Platform;

public sealed class PasswordHasherAdapterTests
{
    private readonly PasswordHasherAdapter _hasher = new();

    [Fact]
    public void Hash_verifies_the_right_password_only()
    {
        var hash = _hasher.Hash("correct-horse-battery");

        Assert.DoesNotContain("correct-horse-battery", hash);
        Assert.True(_hasher.Verify(hash, "correct-horse-battery"));
        Assert.False(_hasher.Verify(hash, "Correct-horse-battery"));
    }

    [Fact]
    public void Same_password_hashes_differently_each_time()
    {
        Assert.NotEqual(_hasher.Hash("a-long-password"), _hasher.Hash("a-long-password"));
    }
}
