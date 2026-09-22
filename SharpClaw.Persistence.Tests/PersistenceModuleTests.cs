using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharpClaw.Contracts.Entities.Core;
using SharpClaw.Contracts.Persistence;
using SharpClaw.Persistence.JSONColdStore;
using SharpClaw.Persistence.PostgreSQL;
using SharpClaw.Persistence.SQLite;
using SharpClaw.Persistence.SQLServer;

namespace SharpClaw.Persistence.Tests;

public sealed class PersistenceModuleTests
{
    [TestCase("JSONColdStore", typeof(JSONColdStorePersistenceProvider))]
    [TestCase("JsonFile", typeof(JSONColdStorePersistenceProvider))]
    [TestCase("PostgreSQL", typeof(PostgreSQLPersistenceProvider))]
    [TestCase("Postgres", typeof(PostgreSQLPersistenceProvider))]
    [TestCase("SQLServer", typeof(SQLServerPersistenceProvider))]
    [TestCase("SqlServer", typeof(SQLServerPersistenceProvider))]
    [TestCase("SQLite", typeof(SQLitePersistenceProvider))]
    public void Modules_RegisterCanonicalProviderAndCompatibilityAliases(
        string key,
        Type expectedType)
    {
        var services = new ServiceCollection();
        new JSONColdStorePersistenceModule().ConfigureServices(services);
        new PostgreSQLPersistenceModule().ConfigureServices(services);
        new SQLServerPersistenceModule().ConfigureServices(services);
        new SQLitePersistenceModule().ConfigureServices(services);
        using var serviceProvider = services.BuildServiceProvider();

        var provider = SharpClawPersistenceProviderResolver.Resolve(
            serviceProvider.GetServices<ISharpClawPersistenceProvider>(),
            key);

        provider.Should().BeOfType(expectedType);
    }

    [TestCaseSource(nameof(RelationalMigrationCases))]
    public void RelationalPackage_OwnsItsInitialMigration(
        Func<DbContext> createContext,
        string expectedMigration)
    {
        using var dbContext = createContext();
        dbContext.Database.GetMigrations().Should().ContainSingle().Which.Should().Be(expectedMigration);
    }

    [Test]
    public async Task JSONColdStore_InitializesAUsableDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sharpclaw-persistence-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Encryption:EncryptDatabase"] = "false",
                })
                .Build();
            var services = new ServiceCollection()
                .AddSingleton(new EncryptionOptions { Key = new byte[32] })
                .BuildServiceProvider();
            var provider = new JSONColdStorePersistenceProvider();
            var options = new SharpClawPersistenceOptions { DataDirectory = directory };
            var builder = new DbContextOptionsBuilder<Runtime.INF.Persistence.SharpClawDbContext>();
            provider.Configure(
                builder,
                new SharpClawPersistenceProviderContext(
                    services,
                    configuration,
                    options,
                    typeof(Runtime.INF.Persistence.SharpClawDbContext),
                    UseMigrations: true));
            await using var dbContext = new Runtime.INF.Persistence.SharpClawDbContext(builder.Options);

            await provider.InitializeAsync(dbContext);

            (await dbContext.Database.CanConnectAsync()).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SQLite_AppliesItsOwnedMigrationToAnEmptyDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SQLite"] = "Data Source=:memory:",
            })
            .Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        var provider = new SQLitePersistenceProvider();
        var options = new SharpClawPersistenceOptions { ProviderKey = provider.Key };
        var builder = new DbContextOptionsBuilder<Runtime.INF.Persistence.SharpClawDbContext>();
        provider.Configure(
            builder,
            new SharpClawPersistenceProviderContext(
                services,
                configuration,
                options,
                typeof(Runtime.INF.Persistence.SharpClawDbContext),
                UseMigrations: true));
        using var dbContext = new Runtime.INF.Persistence.SharpClawDbContext(builder.Options);
        dbContext.Database.OpenConnection();

        dbContext.Database.Migrate();

        dbContext.Database.GetAppliedMigrations()
            .Should().ContainSingle().Which.Should().Be("20260920114922_InitialCreate");
        dbContext.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Test]
    public void SQLite_ModuleOwnsCanonicalDateTimeOffsetMapping()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SQLite"] = "Data Source=:memory:",
            })
            .Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        var provider = new SQLitePersistenceProvider();
        var options = new SharpClawPersistenceOptions { ProviderKey = provider.Key };
        var builder = new DbContextOptionsBuilder<Runtime.INF.Persistence.SharpClawDbContext>();
        provider.Configure(
            builder,
            new SharpClawPersistenceProviderContext(
                services,
                configuration,
                options,
                typeof(Runtime.INF.Persistence.SharpClawDbContext),
                UseMigrations: true));
        using var dbContext = new Runtime.INF.Persistence.SharpClawDbContext(builder.Options);

        var property = dbContext.Model.FindEntityType(typeof(ProviderDB))!
            .FindProperty(nameof(ProviderDB.CreatedAt))!;

        property.GetValueConverter().Should().NotBeNull();
        property.GetValueConverter()!.ProviderClrType.Should().Be(typeof(long));
    }

    [Test]
    public void CanonicalContext_ContainsNoProviderIdentityBranch()
    {
        var source = File.ReadAllText(Path.Combine(
            FindSourceRoot(),
            "SharpClaw.Persistence",
            "SharpClawDbContext.cs"));

        source.Should().NotContain("Microsoft.EntityFrameworkCore.Sqlite");
        source.Should().NotContain("ConfigureForProvider");
    }

    private static IEnumerable<TestCaseData> RelationalMigrationCases()
    {
        yield return new TestCaseData(
            () => (DbContext)new PostgreSQL.DesignTimeFactory().CreateDbContext([]),
            "20260920114914_InitialCreate");
        yield return new TestCaseData(
            () => (DbContext)new SQLServer.DesignTimeFactory().CreateDbContext([]),
            "20260920114918_InitialCreate");
        yield return new TestCaseData(
            () => (DbContext)new SQLite.DesignTimeFactory().CreateDbContext([]),
            "20260920114922_InitialCreate");
    }

    private static string FindSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharpClaw.Persistence.slnx")))
                return directory.FullName;
        }

        throw new AssertionException("The SharpClaw.Persistence source root could not be located.");
    }
}
