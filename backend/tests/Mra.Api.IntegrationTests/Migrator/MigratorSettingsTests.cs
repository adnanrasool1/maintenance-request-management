using Mra.DbMigrator;
using Xunit;

namespace Mra.Api.IntegrationTests.Migrator;

// The migrator fails fast on missing or placeholder configuration; it has no defaults (architecture §10).
public sealed class MigratorSettingsTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        [MigratorSettings.OwnerConnectionStringVariable] = "Server=sqlserver;Database=Mra;User Id=sa;Password=Owner-Pw-1!;TrustServerCertificate=True",
        [MigratorSettings.AppLoginPasswordVariable] = "App-Pw-1!",
        [MigratorSettings.SystemAdminEmailVariable] = "admin@example.local",
        [MigratorSettings.SystemAdminPasswordVariable] = "Admin-Pw-1!",
    };

    private static MigratorSettings Read(Dictionary<string, string?> variables) =>
        MigratorSettings.From(name => variables.GetValueOrDefault(name));

    [Fact]
    public void Valid_configuration_is_read()
    {
        var settings = Read(Valid());

        Assert.Equal("App-Pw-1!", settings.AppLoginPassword);
        Assert.Equal("admin@example.local", settings.SystemAdminEmail);
    }

    [Theory]
    [InlineData(MigratorSettings.OwnerConnectionStringVariable)]
    [InlineData(MigratorSettings.AppLoginPasswordVariable)]
    [InlineData(MigratorSettings.SystemAdminEmailVariable)]
    [InlineData(MigratorSettings.SystemAdminPasswordVariable)]
    public void Missing_or_placeholder_value_fails(string name)
    {
        var missing = Valid();
        missing.Remove(name);
        var placeholder = Valid();
        placeholder[name] = "change-me";

        Assert.Contains(name, Assert.Throws<MigratorConfigurationException>(() => Read(missing)).Message);
        Assert.Contains(name, Assert.Throws<MigratorConfigurationException>(() => Read(placeholder)).Message);
    }

    [Theory]
    [InlineData("Server=sqlserver;User Id=sa;Password=Owner-Pw-1!")]
    [InlineData("Server=sqlserver;Database=Mra;User Id=sa;Password=change-me")]
    public void Owner_connection_string_without_database_or_with_placeholder_password_fails(string connectionString)
    {
        var variables = Valid();
        variables[MigratorSettings.OwnerConnectionStringVariable] = connectionString;

        var error = Assert.Throws<MigratorConfigurationException>(() => Read(variables));

        Assert.DoesNotContain("Owner-Pw-1!", error.Message);
    }

    [Theory]
    [InlineData("Pw'; DROP LOGIN sa;--")]
    [InlineData("Pw\"x")]
    [InlineData("Pw]x")]
    public void App_login_password_with_quotes_or_brackets_fails_without_echoing_it(string password)
    {
        var variables = Valid();
        variables[MigratorSettings.AppLoginPasswordVariable] = password;

        var error = Assert.Throws<MigratorConfigurationException>(() => Read(variables));

        Assert.DoesNotContain(password, error.Message);
    }
}
