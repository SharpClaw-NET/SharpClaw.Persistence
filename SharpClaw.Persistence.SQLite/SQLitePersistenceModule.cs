using Microsoft.Extensions.DependencyInjection;
using SharpClaw.ModuleSDK;

namespace SharpClaw.Persistence.SQLite;

public sealed class SQLitePersistenceModule : ISharpClawModule
{
    public ModuleIdentity Identity { get; } = new(
        "sharpclaw_persistence_sqlite",
        "SQLite Persistence",
        "psq");

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISharpClawPersistenceProvider, SQLitePersistenceProvider>();
    }
}
