using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mra.Infrastructure.Authentication;
using Xunit;

namespace Mra.Api.IntegrationTests.Platform;

public sealed class JwtTests
{
    private static readonly string ValidKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());

    [Fact]
    public async Task Issued_token_validates_and_its_claims_are_read_back_by_CurrentUser()
    {
        var settings = Settings(ValidKey);
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var service = new JwtTokenService(settings, TimeProvider.System);

        var token = service.CreateToken(userId, orgId, "Approver", "alice@acme.example");
        var result = await ValidateAsync(token.Token, settings);

        Assert.True(result.IsValid, result.Exception?.Message);
        var currentUser = CurrentUserFor(new ClaimsPrincipal(result.ClaimsIdentity));
        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal(orgId, currentUser.OrganisationId);
        Assert.Equal("Approver", currentUser.Role);
        Assert.Equal("alice@acme.example", result.Claims[JwtClaimNames.Email]);
        Assert.True(new ClaimsPrincipal(result.ClaimsIdentity).IsInRole("Approver"));
    }

    [Fact]
    public void Token_lasts_60_minutes_from_the_TimeProvider_clock()
    {
        var now = new DateTimeOffset(2026, 9, 26, 10, 15, 30, TimeSpan.Zero);
        var service = new JwtTokenService(Settings(ValidKey), new FixedTimeProvider(now));

        var token = service.CreateToken(Guid.NewGuid(), Guid.NewGuid(), "Requester", "bob@acme.example");

        Assert.Equal(now.UtcDateTime.AddMinutes(60), token.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, token.ExpiresAt.Kind);
        var jwt = new JsonWebToken(token.Token);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
        Assert.Equal(now.UtcDateTime.AddMinutes(60), jwt.ValidTo);
        Assert.Equal("mra-api", jwt.Issuer);
        Assert.Equal(["mra-web"], jwt.Audiences);
    }

    [Fact]
    public void System_admin_token_has_no_org_claim()
    {
        var service = new JwtTokenService(Settings(ValidKey), TimeProvider.System);

        var token = service.CreateToken(Guid.NewGuid(), organisationId: null, "SystemAdmin", "admin@example.local");

        Assert.False(new JsonWebToken(token.Token).TryGetClaim(JwtClaimNames.Organisation, out _));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var settings = Settings(ValidKey);
        var twoHoursAgo = new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(-2));
        var token = new JwtTokenService(settings, twoHoursAgo).CreateToken(Guid.NewGuid(), Guid.NewGuid(), "Requester", "a@b.example");

        var result = await ValidateAsync(token.Token, settings);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var otherKey = Convert.ToBase64String(new byte[32].Select(_ => (byte)7).ToArray());
        var token = new JwtTokenService(Settings(otherKey), TimeProvider.System)
            .CreateToken(Guid.NewGuid(), Guid.NewGuid(), "Approver", "a@b.example");

        var result = await ValidateAsync(token.Token, Settings(ValidKey));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("other-issuer", "mra-web")]
    [InlineData("mra-api", "other-audience")]
    public async Task Token_for_another_issuer_or_audience_is_rejected(string issuer, string audience)
    {
        var token = new JwtTokenService(Settings(ValidKey, issuer, audience), TimeProvider.System)
            .CreateToken(Guid.NewGuid(), Guid.NewGuid(), "Approver", "a@b.example");

        var result = await ValidateAsync(token.Token, Settings(ValidKey));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("change-me")] // the .env.example placeholder is not base64
    public void Missing_or_malformed_key_fails_fast(string? key)
    {
        Assert.Throws<InvalidOperationException>(() => Settings(key));
    }

    [Fact]
    public void Key_shorter_than_32_bytes_fails_fast()
    {
        var shortKey = Convert.ToBase64String(new byte[31]);

        var exception = Assert.Throws<InvalidOperationException>(() => Settings(shortKey));
        Assert.DoesNotContain(shortKey, exception.Message);
    }

    [Theory]
    [InlineData(null, "mra-web")]
    [InlineData("mra-api", null)]
    public void Missing_issuer_or_audience_fails_fast(string? issuer, string? audience)
    {
        Assert.Throws<InvalidOperationException>(() => Settings(ValidKey, issuer, audience));
    }

    [Fact]
    public void AddInfrastructure_fails_fast_without_a_key()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            Mra.Infrastructure.DependencyInjection.AddInfrastructure(
                new Microsoft.Extensions.DependencyInjection.ServiceCollection(), configuration));
    }

    private static JwtSettings Settings(string? key, string? issuer = "mra-api", string? audience = "mra-web") =>
        JwtSettings.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [JwtSettings.SigningKeyKey] = key,
                [JwtSettings.IssuerKey] = issuer,
                [JwtSettings.AudienceKey] = audience,
            })
            .Build());

    // Same handler settings the API's JwtBearer options use: validation parameters from
    // JwtSettings and no inbound claim mapping.
    private static Task<TokenValidationResult> ValidateAsync(string token, JwtSettings settings) =>
        new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, settings.CreateValidationParameters());

    private static CurrentUser CurrentUserFor(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
