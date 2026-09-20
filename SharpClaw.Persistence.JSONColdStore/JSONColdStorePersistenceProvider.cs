using JSONColdStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Contracts.Persistence;

namespace SharpClaw.Persistence.JSONColdStore;

public sealed class JSONColdStorePersistenceProvider : ISharpClawPersistenceProvider
{
    public string Key => "JSONColdStore";
    public IReadOnlyCollection<string> Aliases { get; } = ["JsonFile"];
    public bool IsRelational => false;

    public void Configure(
        DbContextOptionsBuilder optionsBuilder,
        SharpClawPersistenceProviderContext context)
    {
        var settings = JSONColdStoreStorageOptions.FromConfiguration(
            context.Configuration,
            context.UseMigrations
                ? context.Options.DataDirectory
                : GetRegistrationDirectory(context.Options.DataDirectory, context.DbContextType));
        var encryptionKey = settings.EncryptAtRest
            ? JsonColdStoreEncryptionKey.FromBytes(
                context.Services.GetRequiredService<EncryptionOptions>().Key)
            : null;

        optionsBuilder.UseJsonColdStoreDatabase(
            settings.DataDirectory,
            store => ConfigureStore(store, settings, encryptionKey));
    }

    public async ValueTask InitializeAsync(
        DbContext dbContext,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

    private static void ConfigureStore(
        JsonColdStoreOptionsBuilder store,
        JSONColdStoreStorageOptions options,
        JsonColdStoreEncryptionKey? encryptionKey)
    {
        store.UseCompression(options.Compression);
        store.UseStartupMode(options.StartupMode);
        store.UseFullScanPolicy(options.FullScanPolicy);
        store.UseFsyncOnWrite(options.FsyncOnWrite);
        store.UseFlushRetry(options.FlushRetryMaxRetries, TimeSpan.FromMilliseconds(options.FlushRetryBaseDelayMilliseconds));
        store.UseTransactionReplay(options.TransactionReplayMaxRetries);
        store.UseReadRetry(options.ReadRetryMaxRetries, TimeSpan.FromMilliseconds(options.ReadRetryBaseDelayMilliseconds));
        store.UseQuarantine(TimeSpan.FromDays(Math.Max(0, options.QuarantineMaxAgeDays)));
        store.UseIndexMaintenance(TimeSpan.FromMinutes(Math.Max(0, options.IndexRescanIntervalMinutes)));
        store.UseEventLog(options.EnableEventLog, TimeSpan.FromDays(Math.Max(0, options.EventLogRetentionDays)));
        if (options.EnableChecksums)
            store.UseChecksums(verifyOnStartup: true, verifyOnRead: options.VerifyChecksumsOnRead);
        else
            store.DisableChecksums();
        if (options.EnableSnapshots)
            store.UseSnapshots(true, TimeSpan.FromHours(Math.Max(1, options.SnapshotIntervalHours)), Math.Max(1, options.SnapshotRetentionCount));
        if (options.EncryptAtRest)
            store.UseEncryptionKey(encryptionKey ?? throw new InvalidOperationException("JSONColdStore encryption key is unavailable."));
    }

    private static string GetRegistrationDirectory(string root, Type dbContextType)
    {
        var unsafeName = dbContextType.FullName ?? dbContextType.Name;
        var safeName = string.Join("_", unsafeName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(root, "registrations", safeName);
    }
}
