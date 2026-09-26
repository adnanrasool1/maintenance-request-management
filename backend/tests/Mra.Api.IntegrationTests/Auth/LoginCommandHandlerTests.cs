using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Mra.Api.IntegrationTests.Persistence;
using Mra.Api.IntegrationTests.Platform;
using Mra.Application.Auth.Login;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Users;
using Mra.Infrastructure.Authentication;
using Xunit;

namespace Mra.Api.IntegrationTests.Auth;

// PRD FR-1 / contract §2.2: the login handler against the real SQL Server schema, the real
// PBKDF2 hasher and the real token service. The context has no organisation, as for an anonymous caller.
public sealed class LoginCommandHandlerTests(SqlServerFixture fixture)
{
    private const string Password = "correct-horse-battery";

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 15, 30, TimeSpan.Zero);

    private static readonly PasswordHasherAdapter Hasher = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_login_returns_a_token_whose_claims_match_the_user()
    {
        var tenant = await fixture.SeedTenantAsync();
        var user = User.Create(tenant.OrganisationId, SqlServerFixture.UniqueEmail(), Hasher.Hash(Password), Role.Approver);
        await SaveAsync(user);

        var response = await LoginAsync(user.Email, Password);

        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal(user.Email, response.User.Email);
        Assert.Equal("Approver", response.User.Role);
        Assert.Equal(Now.UtcDateTime.AddMinutes(60), response.ExpiresAt);

        var jwt = new JsonWebToken(response.AccessToken);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim(JwtClaimNames.Subject).Value);
        Assert.Equal(tenant.OrganisationId.ToString(), jwt.GetClaim(JwtClaimNames.Organisation).Value);
        Assert.Equal("Approver", jwt.GetClaim(JwtClaimNames.Role).Value);
        Assert.Equal(user.Email, jwt.GetClaim(JwtClaimNames.Email).Value);
    }

    [Fact]
    public async Task Email_is_trimmed_and_matched_case_insensitively()
    {
        var tenant = await fixture.SeedTenantAsync();
        var user = User.Create(tenant.OrganisationId, SqlServerFixture.UniqueEmail(), Hasher.Hash(Password), Role.Requester);
        await SaveAsync(user);

        var response = await LoginAsync($"  {user.Email.ToUpperInvariant()} ", Password);

        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal(user.Email, response.User.Email);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_fail_identically()
    {
        var tenant = await fixture.SeedTenantAsync();
        var user = User.Create(tenant.OrganisationId, SqlServerFixture.UniqueEmail(), Hasher.Hash(Password), Role.Requester);
        await SaveAsync(user);

        var unknownEmail = await Assert.ThrowsAnyAsync<Exception>(() => LoginAsync(SqlServerFixture.UniqueEmail(), Password));
        var wrongPassword = await Assert.ThrowsAnyAsync<Exception>(() => LoginAsync(user.Email, "wrong-password"));

        Assert.IsType<InvalidCredentialsException>(unknownEmail);
        Assert.IsType<InvalidCredentialsException>(wrongPassword);
        Assert.Equal("Invalid email or password.", unknownEmail.Message);
        Assert.Equal(unknownEmail.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task System_admin_can_log_in_and_gets_no_org_claim()
    {
        var admin = User.CreateSystemAdmin(SqlServerFixture.UniqueEmail(), Hasher.Hash(Password));
        await SaveAsync(admin);

        var response = await LoginAsync(admin.Email, Password);

        Assert.Equal(admin.Id, response.User.Id);
        Assert.Equal("SystemAdmin", response.User.Role);
        var jwt = new JsonWebToken(response.AccessToken);
        Assert.False(jwt.TryGetClaim(JwtClaimNames.Organisation, out _));
        Assert.Equal("SystemAdmin", jwt.GetClaim(JwtClaimNames.Role).Value);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData("a@b.example", "")]
    public void Validator_requires_email_and_password(string email, string password)
    {
        var result = new LoginCommandValidator().Validate(new LoginCommand(email, password));

        Assert.False(result.IsValid);
    }

    private async Task SaveAsync(User user)
    {
        await using var db = fixture.CreateContext(organisationId: null);
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<LoginResponse> LoginAsync(string email, string password)
    {
        await using var db = fixture.CreateContext(organisationId: null);
        var handler = new LoginCommandHandler(db, Hasher, new JwtTokenService(CreateJwtSettings(),new FixedTimeProvider(Now)));
        return await handler.Handle(new LoginCommand(email, password), Ct);
    }

    private static JwtSettings CreateJwtSettings() =>
        JwtSettings.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [JwtSettings.SigningKeyKey] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
                [JwtSettings.IssuerKey] = "mra-api",
                [JwtSettings.AudienceKey] = "mra-web",
            })
            .Build());
}
