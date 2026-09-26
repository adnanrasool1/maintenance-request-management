namespace Mra.DbMigrator;

// One-shot console app run before the API (architecture §4.5, §12). Exit code 0 means the
// database is migrated, mra_app is set up and the System Admin exists; anything else is a failure.
// Configuration: see MigratorSettings.
internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var settings = MigratorSettings.FromEnvironment();
            await DatabaseInitializer.RunAsync(settings, Console.Out);
            Console.WriteLine("Migrator finished successfully.");
            return 0;
        }
        catch (MigratorConfigurationException ex)
        {
            Console.Error.WriteLine($"Migrator configuration error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            // EF sensitive-data logging is off, so exception messages carry no parameter values.
            Console.Error.WriteLine($"Migrator failed: {ex}");
            return 1;
        }
    }
}
