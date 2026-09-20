using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.PostgreSQL;

public sealed class PostgreSQLPersistenceProvider : ISharpClawPersistenceProvider
{
    public string Key => "PostgreSQL";
    public IReadOnlyCollection<string> Aliases { get; } = ["Postgres"];
    public bool IsRelational => true;

    public void Configure(DbContextOptionsBuilder optionsBuilder, SharpClawPersistenceProviderContext context)
    {
        var settings = RelationalPersistenceOptions.FromConfiguration(
            context.Configuration,
            Key,
            supportsRetryOnFailure: true,
            "Postgres");
        optionsBuilder.UseNpgsql(context.GetRequiredConnectionString(this), provider =>
        {
            if (context.UseMigrations)
                provider.MigrationsAssembly(typeof(PostgreSQLPersistenceProvider).Assembly.GetName().Name);
            if (settings.CommandTimeoutSeconds is { } timeout)
                provider.CommandTimeout(timeout);
            if (settings.EnableRetryOnFailure)
                provider.EnableRetryOnFailure(settings.MaxRetryCount, TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds), null);
        });
    }
}
