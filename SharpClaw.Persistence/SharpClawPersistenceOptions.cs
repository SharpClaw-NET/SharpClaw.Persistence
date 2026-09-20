using Microsoft.Extensions.Configuration;

namespace SharpClaw.Persistence;

public sealed class SharpClawPersistenceOptions
{
    public const string DefaultProviderKey = "JSONColdStore";

    public string ProviderKey { get; set; } = DefaultProviderKey;
    public string DataDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Data", "database");
    public bool EnableDetailedErrors { get; set; } = true;
    public bool EnableSensitiveDataLogging { get; set; }

    public static SharpClawPersistenceOptions FromConfiguration(
        IConfiguration configuration,
        string? dataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var database = configuration.GetSection("Database");
        var options = new SharpClawPersistenceOptions
        {
            ProviderKey = string.IsNullOrWhiteSpace(database["Provider"])
                ? DefaultProviderKey
                : database["Provider"]!.Trim(),
            DataDirectory = string.IsNullOrWhiteSpace(dataDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Data", "database")
                : Path.GetFullPath(dataDirectory),
            EnableDetailedErrors = ReadBool(
                database["EnableDetailedErrors"],
                defaultValue: true,
                "Database:EnableDetailedErrors"),
            EnableSensitiveDataLogging = ReadBool(
                database["EnableSensitiveDataLogging"],
                defaultValue: false,
                "Database:EnableSensitiveDataLogging"),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProviderKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(DataDirectory);
    }

    public static bool ReadBool(string? value, bool defaultValue, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (bool.TryParse(value, out var parsed))
            return parsed;
        throw new InvalidOperationException($"{key} must be true or false.");
    }

    public static int ReadInt(string? value, int defaultValue, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (int.TryParse(value, out var parsed))
            return parsed;
        throw new InvalidOperationException($"{key} must be an integer.");
    }

    public static int? ReadNullableInt(string? value, int? defaultValue, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (int.TryParse(value, out var parsed))
            return parsed;
        throw new InvalidOperationException($"{key} must be an integer.");
    }

    public static TEnum ReadEnum<TEnum>(string? value, TEnum defaultValue, string key)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"{key} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }
}
