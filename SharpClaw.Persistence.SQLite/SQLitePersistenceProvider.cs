using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.SQLite;

public sealed class SQLitePersistenceProvider : ISharpClawPersistenceProvider
{
    public string Key => "SQLite";
    public IReadOnlyCollection<string> Aliases { get; } = [];
    public bool IsRelational => true;

    public void Configure(DbContextOptionsBuilder optionsBuilder, SharpClawPersistenceProviderContext context)
    {
        var settings = RelationalPersistenceOptions.FromConfiguration(
            context.Configuration,
            Key,
            supportsRetryOnFailure: false);
        optionsBuilder.UseSqlite(context.GetRequiredConnectionString(this), provider =>
        {
            if (context.UseMigrations)
                provider.MigrationsAssembly(typeof(SQLitePersistenceProvider).Assembly.GetName().Name);
            if (settings.CommandTimeoutSeconds is { } timeout)
                provider.CommandTimeout(timeout);
        });
    }
}
