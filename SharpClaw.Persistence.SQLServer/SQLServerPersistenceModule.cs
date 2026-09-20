using Microsoft.Extensions.DependencyInjection;
using SharpClaw.ModuleSDK;

namespace SharpClaw.Persistence.SQLServer;

public sealed class SQLServerPersistenceModule : ISharpClawModule
{
    public ModuleIdentity Identity { get; } = new(
        "sharpclaw_persistence_sqlserver",
        "SQL Server Persistence",
        "pss");

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISharpClawPersistenceProvider, SQLServerPersistenceProvider>();
    }
}
