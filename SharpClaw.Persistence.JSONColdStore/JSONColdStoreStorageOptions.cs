using JSONColdStore;
using Microsoft.Extensions.Configuration;

namespace SharpClaw.Persistence.JSONColdStore;

public sealed class JSONColdStoreStorageOptions
{
    public required string DataDirectory { get; init; }
    public bool EncryptAtRest { get; set; } = true;
    public JsonColdStoreCompression Compression { get; set; } = JsonColdStoreCompression.Brotli;
    public JsonColdStoreStartupMode StartupMode { get; set; } = JsonColdStoreStartupMode.MetadataOnly;
    public JsonColdStoreScanPolicy FullScanPolicy { get; set; } = JsonColdStoreScanPolicy.AllowSilentScans;
    public bool FsyncOnWrite { get; set; } = true;
    public int FlushRetryMaxRetries { get; set; } = 3;
    public int FlushRetryBaseDelayMilliseconds { get; set; } = 200;
    public int TransactionReplayMaxRetries { get; set; } = 3;
    public int ReadRetryMaxRetries { get; set; } = 3;
    public int ReadRetryBaseDelayMilliseconds { get; set; } = 25;
    public int IndexRescanIntervalMinutes { get; set; } = 60;
    public int QuarantineMaxAgeDays { get; set; } = 30;
    public bool EnableChecksums { get; set; } = true;
    public bool VerifyChecksumsOnRead { get; set; }
    public bool EnableEventLog { get; set; }
    public int EventLogRetentionDays { get; set; } = 7;
    public bool EnableSnapshots { get; set; }
    public int SnapshotIntervalHours { get; set; } = 24;
    public int SnapshotRetentionCount { get; set; } = 3;

    public static JSONColdStoreStorageOptions FromConfiguration(
        IConfiguration configuration,
        string dataDirectory)
    {
        var options = new JSONColdStoreStorageOptions { DataDirectory = dataDirectory };
        options.EncryptAtRest = SharpClawPersistenceOptions.ReadBool(
            configuration["Encryption:EncryptDatabase"],
            options.EncryptAtRest,
            "Encryption:EncryptDatabase");
        Apply(configuration.GetSection("Database:JsonFile"), options, "Database:JsonFile");
        Apply(configuration.GetSection("Database:JSONColdStore"), options, "Database:JSONColdStore");
        options.Validate();
        return options;
    }

    private static void Apply(IConfiguration section, JSONColdStoreStorageOptions options, string path)
    {
        options.Compression = SharpClawPersistenceOptions.ReadEnum(section["Compression"], options.Compression, $"{path}:Compression");
        options.StartupMode = SharpClawPersistenceOptions.ReadEnum(section["StartupMode"], options.StartupMode, $"{path}:StartupMode");
        options.FullScanPolicy = SharpClawPersistenceOptions.ReadEnum(section["FullScanPolicy"], options.FullScanPolicy, $"{path}:FullScanPolicy");
        options.FsyncOnWrite = SharpClawPersistenceOptions.ReadBool(section["FsyncOnWrite"], options.FsyncOnWrite, $"{path}:FsyncOnWrite");
        options.IndexRescanIntervalMinutes = SharpClawPersistenceOptions.ReadInt(section["IndexRescanIntervalMinutes"], options.IndexRescanIntervalMinutes, $"{path}:IndexRescanIntervalMinutes");
        options.QuarantineMaxAgeDays = SharpClawPersistenceOptions.ReadInt(section["QuarantineMaxAgeDays"], options.QuarantineMaxAgeDays, $"{path}:QuarantineMaxAgeDays");
        options.EnableChecksums = SharpClawPersistenceOptions.ReadBool(section["EnableChecksums"], options.EnableChecksums, $"{path}:EnableChecksums");
        options.VerifyChecksumsOnRead = SharpClawPersistenceOptions.ReadBool(section["VerifyChecksumsOnRead"], options.VerifyChecksumsOnRead, $"{path}:VerifyChecksumsOnRead");
        options.EnableEventLog = SharpClawPersistenceOptions.ReadBool(section["EnableEventLog"], options.EnableEventLog, $"{path}:EnableEventLog");
        options.EventLogRetentionDays = SharpClawPersistenceOptions.ReadInt(section["EventLogRetentionDays"], options.EventLogRetentionDays, $"{path}:EventLogRetentionDays");
        options.EnableSnapshots = SharpClawPersistenceOptions.ReadBool(section["EnableSnapshots"], options.EnableSnapshots, $"{path}:EnableSnapshots");
        options.SnapshotIntervalHours = SharpClawPersistenceOptions.ReadInt(section["SnapshotIntervalHours"], options.SnapshotIntervalHours, $"{path}:SnapshotIntervalHours");
        options.SnapshotRetentionCount = SharpClawPersistenceOptions.ReadInt(section["SnapshotRetentionCount"], options.SnapshotRetentionCount, $"{path}:SnapshotRetentionCount");
        options.FlushRetryMaxRetries = SharpClawPersistenceOptions.ReadInt(section["FlushRetryMaxRetries"], options.FlushRetryMaxRetries, $"{path}:FlushRetryMaxRetries");
        options.FlushRetryBaseDelayMilliseconds = SharpClawPersistenceOptions.ReadInt(section["FlushRetryBaseDelayMilliseconds"], options.FlushRetryBaseDelayMilliseconds, $"{path}:FlushRetryBaseDelayMilliseconds");
        options.TransactionReplayMaxRetries = SharpClawPersistenceOptions.ReadInt(section["TransactionReplayMaxRetries"], options.TransactionReplayMaxRetries, $"{path}:TransactionReplayMaxRetries");
        options.ReadRetryMaxRetries = SharpClawPersistenceOptions.ReadInt(section["ReadRetryMaxRetries"], options.ReadRetryMaxRetries, $"{path}:ReadRetryMaxRetries");
        options.ReadRetryBaseDelayMilliseconds = SharpClawPersistenceOptions.ReadInt(section["ReadRetryBaseDelayMilliseconds"], options.ReadRetryBaseDelayMilliseconds, $"{path}:ReadRetryBaseDelayMilliseconds");
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DataDirectory))
            throw new InvalidOperationException("JSONColdStore data directory must not be blank.");
        if (FlushRetryMaxRetries <= 0 || FlushRetryBaseDelayMilliseconds <= 0
            || TransactionReplayMaxRetries <= 0 || ReadRetryMaxRetries <= 0
            || ReadRetryBaseDelayMilliseconds <= 0)
        {
            throw new InvalidOperationException("JSONColdStore retry counts and delays must be greater than zero.");
        }
        if (IndexRescanIntervalMinutes < 0 || QuarantineMaxAgeDays < 0 || EventLogRetentionDays < 0)
            throw new InvalidOperationException("JSONColdStore maintenance intervals must be zero or greater.");
        if (SnapshotIntervalHours <= 0 || SnapshotRetentionCount <= 0)
            throw new InvalidOperationException("JSONColdStore snapshot settings must be greater than zero.");
    }
}
