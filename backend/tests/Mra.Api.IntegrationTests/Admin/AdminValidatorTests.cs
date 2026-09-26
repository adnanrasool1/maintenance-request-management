using Mra.Application.Admin.CreateOrganisation;
using Mra.Application.Admin.CreateSite;
using Mra.Application.Admin.CreateUser;
using Mra.Application.Admin.SetThreshold;
using Xunit;

namespace Mra.Api.IntegrationTests.Admin;

// T6 input rules (contract §3.1–§3.4, D-4, D-5). No database needed.
public sealed class AdminValidatorTests
{
    public static TheoryData<string, bool> Thresholds => new()
    {
        { "0", true },
        { "0.01", true },
        { "7500.5", true },
        { "7500.50", true },
        { "7500.500", true }, // same value as 7500.5: the check is on the value, not the scale
        { "1000000.00", true },
        { "1000000.01", false },
        { "-0.01", false },
        { "1.005", false },
        { "0.001", false },
    };

    [Theory]
    [MemberData(nameof(Thresholds))]
    public void Threshold_must_be_between_0_and_1_000_000_with_at_most_2_decimal_places(string value, bool valid)
    {
        var threshold = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(valid, new SetThresholdValidator().Validate(new SetThresholdCommand(threshold)).IsValid);
        Assert.Equal(valid, new CreateOrganisationValidator().Validate(ValidOrganisation() with { ApprovalThreshold = threshold }).IsValid);
    }

    [Fact]
    public void Threshold_is_required()
    {
        var result = new SetThresholdValidator().Validate(new SetThresholdCommand(null));

        Assert.Equal("ApprovalThreshold", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Valid_organisation_passes()
    {
        Assert.True(new CreateOrganisationValidator().Validate(ValidOrganisation()).IsValid);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("   ", "Name")]
    public void Organisation_name_is_required(string name, string property)
    {
        var result = new CreateOrganisationValidator().Validate(ValidOrganisation() with { Name = name });

        Assert.Equal(property, Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Names_are_limited_to_200_characters_after_trimming()
    {
        var validator = new CreateSiteValidator();

        Assert.True(validator.Validate(new CreateSiteCommand("  " + new string('a', 200) + "  ")).IsValid);
        Assert.False(validator.Validate(new CreateSiteCommand(new string('a', 201))).IsValid);
        Assert.False(validator.Validate(new CreateSiteCommand(" ")).IsValid);
    }

    [Theory]
    [InlineData("bob@acme.example", true)]
    [InlineData("  bob@acme.example  ", true)]
    [InlineData("not-an-email", false)]
    [InlineData("Bob <bob@acme.example>", false)]
    [InlineData("", false)]
    public void Email_must_be_a_valid_address(string email, bool valid)
    {
        Assert.Equal(valid, new CreateUserValidator().Validate(new CreateUserCommand(email, "password1", "Requester")).IsValid);
    }

    [Fact]
    public void Email_is_limited_to_256_characters()
    {
        // Short local part and 50-character labels, so only the total length is under test.
        var labels = string.Join('.', Enumerable.Repeat(new string('a', 50), 4));
        var ok = $"bob@{labels}.{new string('b', 48)}";
        var tooLong = $"bob@{labels}.{new string('b', 49)}";
        Assert.Equal(256, ok.Length);

        Assert.True(new CreateUserValidator().Validate(new CreateUserCommand(ok, "password1", "Requester")).IsValid);
        Assert.False(new CreateUserValidator().Validate(new CreateUserCommand(tooLong, "password1", "Requester")).IsValid);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Password_must_be_8_to_128_characters(int length, bool valid)
    {
        var password = new string('p', length);

        Assert.Equal(valid, new CreateUserValidator().Validate(new CreateUserCommand("bob@acme.example", password, "Requester")).IsValid);
        Assert.Equal(valid, new CreateOrganisationValidator().Validate(ValidOrganisation() with { AdminPassword = password }).IsValid);
    }

    [Theory]
    [InlineData("Requester", true)]
    [InlineData("approver", true)]
    [InlineData("TenantAdmin", false)]
    [InlineData("SystemAdmin", false)]
    [InlineData("3", false)]
    [InlineData("", false)]
    public void Tenant_admin_may_create_only_requesters_and_approvers(string role, bool valid)
    {
        var result = new CreateUserValidator().Validate(new CreateUserCommand("bob@acme.example", "password1", role));

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal("Role", Assert.Single(result.Errors).PropertyName);
        }
    }

    private static CreateOrganisationCommand ValidOrganisation() =>
        new("Acme Facilities", 5000.00m, "admin@acme.example", "a-long-password");
}
