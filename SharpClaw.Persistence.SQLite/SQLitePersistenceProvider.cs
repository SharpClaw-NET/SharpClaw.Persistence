using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SharpClaw.Persistence.SQLite;

public sealed class SQLitePersistenceProvider : ISharpClawPersistenceProvider
{
    private static readonly Lazy<bool> SQLiteInitialized = new(
        InitializeSQLite,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Key => "SQLite";
    public IReadOnlyCollection<string> Aliases { get; } = [];
    public bool IsRelational => true;

    public void Configure(DbContextOptionsBuilder optionsBuilder, SharpClawPersistenceProviderContext context)
    {
        _ = SQLiteInitialized.Value;
        ConfigureCanonicalModel(optionsBuilder);
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

    internal static void ConfigureCanonicalModel(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<IModelCustomizer, SQLiteSharpClawModelCustomizer>();

    private static bool InitializeSQLite()
    {
        SQLitePCL.Batteries_V2.Init();
        return true;
    }
}
