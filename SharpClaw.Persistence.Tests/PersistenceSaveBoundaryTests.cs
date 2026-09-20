using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using SharpClaw.Contracts.Entities.Core;
using SharpClaw.Runtime.INF.Persistence;

namespace SharpClaw.Persistence.Tests;

public sealed class PersistenceSaveBoundaryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ExplicitAsyncSaveOverloadRunsOneCoordinator(bool acceptAllChangesOnSuccess)
    {
        var coordinator = new RecordingSaveCoordinator(returnValue: 17);
        await using var fixture = await CreateDatabaseAsync(coordinator);
        fixture.Context.ConfigurationEntries.Add(new ConfigurationEntryDB
        {
            SourceId = "save-boundary",
            Key = acceptAllChangesOnSuccess ? "accept" : "reject",
            Value = "value",
        });

        var saved = await fixture.Context.SaveChangesAsync(acceptAllChangesOnSuccess);

        saved.Should().Be(17);
        coordinator.CoordinatorCalls.Should().Be(1);
        coordinator.AcceptAllChangesValues.Should().Equal(acceptAllChangesOnSuccess);
        (await fixture.Context.ConfigurationEntries.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task DefaultAsyncSaveOverloadRunsOneCoordinatorWithAcceptChangesEnabled()
    {
        var coordinator = new RecordingSaveCoordinator(returnValue: 23);
        await using var fixture = await CreateDatabaseAsync(coordinator);

        var saved = await fixture.Context.SaveChangesAsync();

        saved.Should().Be(23);
        coordinator.CoordinatorCalls.Should().Be(1);
        coordinator.AcceptAllChangesValues.Should().Equal(true);
    }

    [Test]
    public async Task AsyncSaveWithoutTheRuntimeCoordinatorCannotWrite()
    {
        await using var fixture = await CreateDatabaseAsync(saveCoordinator: null);
        fixture.Context.ConfigurationEntries.Add(new ConfigurationEntryDB
        {
            SourceId = "save-boundary",
            Key = "missing-coordinator",
        });

        Func<Task> defaultOverload = async () => await fixture.Context.SaveChangesAsync();
        Func<Task> explicitOverload = async () =>
            await fixture.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true);

        await defaultOverload.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*save coordinator is not configured*");
        await explicitOverload.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*save coordinator is not configured*");
        (await fixture.Context.ConfigurationEntries.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task SynchronousSaveOverloadsAreRejectedBeforeTheCoordinator()
    {
        var coordinator = new RecordingSaveCoordinator();
        await using var fixture = await CreateDatabaseAsync(coordinator);
        fixture.Context.ConfigurationEntries.Add(new ConfigurationEntryDB
        {
            SourceId = "save-boundary",
            Key = "synchronous",
        });

        Action defaultOverload = () => fixture.Context.SaveChanges();
        Action explicitOverload = () => fixture.Context.SaveChanges(acceptAllChangesOnSuccess: true);

        defaultOverload.Should().Throw<NotSupportedException>()
            .WithMessage("*must use SaveChangesAsync*");
        explicitOverload.Should().Throw<NotSupportedException>()
            .WithMessage("*must use SaveChangesAsync*");
        coordinator.CoordinatorCalls.Should().Be(0);
        (await fixture.Context.ConfigurationEntries.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Test]
    public void PublicPackageApiExposesNoSaveTerminalOrSubclassEscape()
    {
        typeof(SharpClawDbContext).IsSealed.Should().BeTrue();
        var declaredPublicMethods = typeof(SharpClawDbContext).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        declaredPublicMethods.Should().NotContain(method =>
            method.Name.Contains("Terminal", StringComparison.Ordinal)
            || method.Name.Contains("ThroughKernel", StringComparison.Ordinal));
        declaredPublicMethods.Where(method => method.Name == nameof(DbContext.SaveChanges))
            .Should().HaveCount(2);
        declaredPublicMethods.Where(method => method.Name == nameof(DbContext.SaveChangesAsync))
            .Should().HaveCount(2);

        var terminal = typeof(SharpClawDbContext).GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Should().ContainSingle(method => method.Name == "SaveChangesTerminalAsync")
            .Which;
        terminal.IsAssembly.Should().BeTrue();
        typeof(SharpClawDbContext).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Should().Equal("SharpClaw.Runtime.INF");

        var coordinatorMethod = typeof(ISharpClawPersistenceSaveCoordinator)
            .GetMethod(nameof(ISharpClawPersistenceSaveCoordinator.SaveChangesAsync));
        coordinatorMethod.Should().NotBeNull();
        coordinatorMethod!.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().Equal(
                typeof(SharpClawDbContext),
                typeof(bool),
                typeof(CancellationToken));
        coordinatorMethod.GetParameters()
            .Should().NotContain(parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    [Test]
    public void ContextSourceContainsOneInternalBaseSaveTerminal()
    {
        var sourceRoot = FindSourceRoot();
        var source = File.ReadAllText(Path.Combine(
            sourceRoot,
            "SharpClaw.Persistence",
            "SharpClawDbContext.cs"));

        source.Should().Contain("internal async Task<int> SaveChangesTerminalAsync(");
        source.Should().Contain("base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken)");
        source.Should().NotContain("base.SaveChanges(");
    }

    private static async Task<DatabaseFixture> CreateDatabaseAsync(
        ISharpClawPersistenceSaveCoordinator? saveCoordinator)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SharpClawDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new SharpClawDbContext(options, saveCoordinator);
        await context.Database.EnsureCreatedAsync();
        return new DatabaseFixture(context, connection);
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

    private sealed class RecordingSaveCoordinator(int returnValue = 0)
        : ISharpClawPersistenceSaveCoordinator
    {
        public int CoordinatorCalls { get; private set; }
        public List<bool> AcceptAllChangesValues { get; } = [];

        public Task<int> SaveChangesAsync(
            SharpClawDbContext dbContext,
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            CoordinatorCalls++;
            AcceptAllChangesValues.Add(acceptAllChangesOnSuccess);
            return Task.FromResult(returnValue);
        }
    }

    private sealed class DatabaseFixture(
        SharpClawDbContext context,
        SqliteConnection connection) : IAsyncDisposable
    {
        public SharpClawDbContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
