using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SharpClaw.Persistence;

public interface ISharpClawPersistenceProvider
{
    string Key { get; }
    IReadOnlyCollection<string> Aliases { get; }
    bool IsRelational { get; }

    void Configure(
        DbContextOptionsBuilder optionsBuilder,
        SharpClawPersistenceProviderContext context);

    ValueTask InitializeAsync(
        DbContext dbContext,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

public sealed record SharpClawPersistenceProviderContext(
    IServiceProvider Services,
    IConfiguration Configuration,
    SharpClawPersistenceOptions Options,
    Type DbContextType,
    bool UseMigrations)
{
    public string GetRequiredConnectionString(ISharpClawPersistenceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var names = new[] { Options.ProviderKey, provider.Key }
            .Concat(provider.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var name in names)
        {
            var connectionString = Configuration[$"ConnectionStrings:{name}"];
            if (!string.IsNullOrWhiteSpace(connectionString))
                return connectionString;
        }

        throw new InvalidOperationException(
            $"A connection string is required for persistence provider '{provider.Key}'. " +
            $"Configure one of: {string.Join(", ", names.Select(name => $"ConnectionStrings:{name}"))}.");
    }
}

public static class SharpClawPersistenceProviderResolver
{
    public static ISharpClawPersistenceProvider Resolve(
        IEnumerable<ISharpClawPersistenceProvider> providers,
        string providerKey)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var matches = providers
            .Where(provider => Matches(provider, providerKey))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"Persistence provider '{providerKey}' is not installed. " +
                "Install and enable its SharpClaw.Persistence.* module package."),
            _ => throw new InvalidOperationException(
                $"More than one persistence module claims provider key '{providerKey}'."),
        };
    }

    private static bool Matches(ISharpClawPersistenceProvider provider, string providerKey) =>
        string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase)
        || provider.Aliases.Any(alias =>
            string.Equals(alias, providerKey, StringComparison.OrdinalIgnoreCase));
}
