using Microsoft.Extensions.DependencyInjection;
using SharpClaw.ModuleSDK;

namespace SharpClaw.Persistence.JSONColdStore;

public sealed class JSONColdStorePersistenceModule : ISharpClawModule
{
    public ModuleIdentity Identity { get; } = new(
        "sharpclaw_persistence_jsoncoldstore",
        "JSONColdStore Persistence",
        "psj");

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISharpClawPersistenceProvider, JSONColdStorePersistenceProvider>();
    }
}
