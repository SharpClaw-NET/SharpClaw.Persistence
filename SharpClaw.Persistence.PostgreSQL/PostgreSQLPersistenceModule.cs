using Microsoft.Extensions.DependencyInjection;
using SharpClaw.ModuleSDK;

namespace SharpClaw.Persistence.PostgreSQL;

public sealed class PostgreSQLPersistenceModule : ISharpClawModule
{
    public ModuleIdentity Identity { get; } = new(
        "sharpclaw_persistence_postgresql",
        "PostgreSQL Persistence",
        "psp");

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISharpClawPersistenceProvider, PostgreSQLPersistenceProvider>();
    }
}
