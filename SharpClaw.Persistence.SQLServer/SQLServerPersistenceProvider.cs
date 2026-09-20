using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.SQLServer;

public sealed class SQLServerPersistenceProvider : ISharpClawPersistenceProvider
{
    public string Key => "SQLServer";
    public IReadOnlyCollection<string> Aliases { get; } = ["SqlServer"];
    public bool IsRelational => true;

    public void Configure(DbContextOptionsBuilder optionsBuilder, SharpClawPersistenceProviderContext context)
    {
        var settings = RelationalPersistenceOptions.FromConfiguration(
            context.Configuration,
            Key,
            supportsRetryOnFailure: true,
            "SqlServer");
        optionsBuilder.UseSqlServer(context.GetRequiredConnectionString(this), provider =>
        {
            if (context.UseMigrations)
                provider.MigrationsAssembly(typeof(SQLServerPersistenceProvider).Assembly.GetName().Name);
            if (settings.CommandTimeoutSeconds is { } timeout)
                provider.CommandTimeout(timeout);
            if (settings.EnableRetryOnFailure)
                provider.EnableRetryOnFailure(settings.MaxRetryCount, TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds), null);
        });
    }
}
