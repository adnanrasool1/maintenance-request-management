using Microsoft.Data.SqlClient;

namespace Mra.DbMigrator;

// Configuration of the migrator, read from environment variables only (architecture §10).
// Every variable is required and has no default; a missing or "change-me" value stops the run.
//
//   ConnectionStrings__Owner  Owner (sa) connection string, including Database=<name>.
//                             docker-compose composes it from MSSQL_SA_PASSWORD.
//   MRA_APP_PASSWORD          Password the restricted mra_app login is created or reset with.
//   SYSTEM_ADMIN_EMAIL        Email of the System Admin seeded on first run.
//   SYSTEM_ADMIN_PASSWORD     Password of that System Admin (stored only as a hash).
public sealed record MigratorSettings(
    string OwnerConnectionString,
    string AppLoginPassword,
    string SystemAdminEmail,
    string SystemAdminPassword)
{
    public const string OwnerConnectionStringVariable = "ConnectionStrings__Owner";
    public const string AppLoginPasswordVariable = "MRA_APP_PASSWORD";
    public const string SystemAdminEmailVariable = "SYSTEM_ADMIN_EMAIL";
    public const string SystemAdminPasswordVariable = "SYSTEM_ADMIN_PASSWORD";

    private const string Placeholder = "change-me";

    // SQL Server limits a login password to 128 characters.
    private const int MaxLoginPasswordLength = 128;

    public static MigratorSettings FromEnvironment() => From(Environment.GetEnvironmentVariable);

    public static MigratorSettings From(Func<string, string?> getVariable)
    {
        var ownerConnectionString = Require(getVariable, OwnerConnectionStringVariable);
        var appLoginPassword = Require(getVariable, AppLoginPasswordVariable);
        var systemAdminEmail = Require(getVariable, SystemAdminEmailVariable);
        var systemAdminPassword = Require(getVariable, SystemAdminPasswordVariable);

        ValidateOwnerConnectionString(ownerConnectionString);
        ValidateAppLoginPassword(appLoginPassword);

        return new MigratorSettings(ownerConnectionString, appLoginPassword, systemAdminEmail, systemAdminPassword);
    }

    private static string Require(Func<string, string?> getVariable, string name)
    {
        var value = getVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MigratorConfigurationException($"{name} is not set.");
        }

        if (value.Trim().Equals(Placeholder, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigratorConfigurationException($"{name} still has the placeholder value; run infra/setup.sh or setup.ps1.");
        }

        return value;
    }

    private static void ValidateOwnerConnectionString(string connectionString)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // The parser's message could quote part of the string, which holds the password.
            throw new MigratorConfigurationException($"{OwnerConnectionStringVariable} is not a valid connection string.");
        }

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new MigratorConfigurationException($"{OwnerConnectionStringVariable} must name the database (Database=...).");
        }

        if (builder.Password.Equals(Placeholder, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigratorConfigurationException($"{OwnerConnectionStringVariable} still has the placeholder password; run infra/setup.sh or setup.ps1.");
        }
    }

    // CREATE/ALTER LOGIN cannot take the password as a parameter, so it ends up inside a
    // statement. It is quoted server-side with QUOTENAME, and quotes are rejected here as well.
    private static void ValidateAppLoginPassword(string password)
    {
        if (password.Length > MaxLoginPasswordLength)
        {
            throw new MigratorConfigurationException($"{AppLoginPasswordVariable} must be at most {MaxLoginPasswordLength} characters.");
        }

        if (password.Any(c => c is '\'' or '"' or '[' or ']' || char.IsControl(c)))
        {
            throw new MigratorConfigurationException($"{AppLoginPasswordVariable} must not contain quotes, brackets or control characters.");
        }
    }
}

public sealed class MigratorConfigurationException(string message) : Exception(message);
