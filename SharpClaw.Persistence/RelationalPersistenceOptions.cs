using Microsoft.Extensions.Configuration;

namespace SharpClaw.Persistence;

public sealed class RelationalPersistenceOptions(bool supportsRetryOnFailure)
{
    public bool SupportsRetryOnFailure { get; } = supportsRetryOnFailure;
    public int? CommandTimeoutSeconds { get; set; }
    public bool EnableRetryOnFailure { get; set; }
    public int MaxRetryCount { get; set; } = 6;
    public int MaxRetryDelaySeconds { get; set; } = 30;

    public static RelationalPersistenceOptions FromConfiguration(
        IConfiguration configuration,
        string providerSection,
        bool supportsRetryOnFailure,
        params string[] aliasSections)
    {
        var options = new RelationalPersistenceOptions(supportsRetryOnFailure);
        Apply(configuration.GetSection("Database:Relational"), options, "Database:Relational");
        foreach (var alias in aliasSections)
            Apply(configuration.GetSection($"Database:{alias}"), options, $"Database:{alias}");
        Apply(
            configuration.GetSection($"Database:{providerSection}"),
            options,
            $"Database:{providerSection}");
        options.Validate(providerSection);
        return options;
    }

    private static void Apply(
        IConfiguration section,
        RelationalPersistenceOptions options,
        string path)
    {
        options.CommandTimeoutSeconds = SharpClawPersistenceOptions.ReadNullableInt(
            section["CommandTimeoutSeconds"],
            options.CommandTimeoutSeconds,
            $"{path}:CommandTimeoutSeconds");
        if (!options.SupportsRetryOnFailure)
            return;

        options.EnableRetryOnFailure = SharpClawPersistenceOptions.ReadBool(
            section["EnableRetryOnFailure"],
            options.EnableRetryOnFailure,
            $"{path}:EnableRetryOnFailure");
        options.MaxRetryCount = SharpClawPersistenceOptions.ReadInt(
            section["MaxRetryCount"],
            options.MaxRetryCount,
            $"{path}:MaxRetryCount");
        options.MaxRetryDelaySeconds = SharpClawPersistenceOptions.ReadInt(
            section["MaxRetryDelaySeconds"],
            options.MaxRetryDelaySeconds,
            $"{path}:MaxRetryDelaySeconds");
    }

    private void Validate(string providerName)
    {
        if (CommandTimeoutSeconds is <= 0)
            throw new InvalidOperationException($"Database:{providerName}:CommandTimeoutSeconds must be greater than zero when set.");
        if (MaxRetryCount <= 0)
            throw new InvalidOperationException($"Database:{providerName}:MaxRetryCount must be greater than zero.");
        if (MaxRetryDelaySeconds <= 0)
            throw new InvalidOperationException($"Database:{providerName}:MaxRetryDelaySeconds must be greater than zero.");
    }
}
