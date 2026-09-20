using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace SharpClaw.Persistence.Tests;

public sealed class PackageIdentityTests
{
    private const string ExpectedLicense = "AGPL-3.0-or-later";
    private const string RepositoryUrl = "https://github.com/SharpClaw-NET/SharpClaw.Persistence";

    private static readonly ModuleIdentity[] Modules =
    [
        new(
            "SharpClaw.Persistence.JSONColdStore",
            "sharpclaw_persistence_jsoncoldstore",
            "SharpClaw.Persistence.JSONColdStore.dll",
            "SharpClaw.Persistence.JSONColdStore.JSONColdStorePersistenceModule"),
        new(
            "SharpClaw.Persistence.PostgreSQL",
            "sharpclaw_persistence_postgresql",
            "SharpClaw.Persistence.PostgreSQL.dll",
            "SharpClaw.Persistence.PostgreSQL.PostgreSQLPersistenceModule"),
        new(
            "SharpClaw.Persistence.SQLServer",
            "sharpclaw_persistence_sqlserver",
            "SharpClaw.Persistence.SQLServer.dll",
            "SharpClaw.Persistence.SQLServer.SQLServerPersistenceModule"),
        new(
            "SharpClaw.Persistence.SQLite",
            "sharpclaw_persistence_sqlite",
            "SharpClaw.Persistence.SQLite.dll",
            "SharpClaw.Persistence.SQLite.SQLitePersistenceModule"),
    ];

    [Test]
    public void SourceReleaseIdentityIsInternallyConsistent()
    {
        var sourceRoot = FindSourceRoot();
        var version = ReadRepositoryVersion(sourceRoot);
        var license = File.ReadAllText(Path.Combine(sourceRoot, "LICENSE.md"));

        license.Should().Contain("included with SharpClaw.Persistence");
        license.Should().Contain(RepositoryUrl);
        license.Should().NotContain("SharpClaw.ProviderIntegrations");

        foreach (var module in Modules)
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(sourceRoot, module.PackageId, "package.json")));
            var root = manifest.RootElement;
            root.GetProperty("id").GetString().Should().Be(module.SourceId);
            root.GetProperty("version").GetString().Should().Be(version);
            root.GetProperty("license").GetString().Should().Be(ExpectedLicense);
            root.GetProperty("entryAssembly").GetString().Should().Be(module.EntryAssembly);
            root.GetProperty("entryType").GetString().Should().Be(module.EntryType);
        }
    }

    [Test]
    public void PackedReleaseIdentityMatchesSourceAndReviewedCommit()
    {
        var packageRoot = Environment.GetEnvironmentVariable(
            "SHARPCLAW_PERSISTENCE_PACKAGE_ROOT");
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            Assert.Ignore(
                "Set SHARPCLAW_PERSISTENCE_PACKAGE_ROOT after packing to validate immutable artifacts.");
        }

        var expectedVersion = Environment.GetEnvironmentVariable(
            "SHARPCLAW_PERSISTENCE_PACKAGE_VERSION");
        var expectedCommit = Environment.GetEnvironmentVariable(
            "SHARPCLAW_PERSISTENCE_COMMIT");
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommit);

        var sourceRoot = FindSourceRoot();
        ReadRepositoryVersion(sourceRoot).Should().Be(expectedVersion);
        var packages = new[] { "SharpClaw.Persistence" }
            .Concat(Modules.Select(module => module.PackageId))
            .ToArray();

        foreach (var packageId in packages)
        {
            var packagePath = Path.Combine(
                packageRoot,
                $"{packageId}.{expectedVersion}.nupkg");
            File.Exists(packagePath).Should().BeTrue(
                $"the frozen package '{packageId}' must exist");
            using var archive = ZipFile.OpenRead(packagePath);
            archive.Entries.Should().OnlyContain(entry => IsSafeArchivePath(entry.FullName));

            var nuspecEntry = archive.Entries.Should()
                .ContainSingle(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal))
                .Which;
            var nuspec = XDocument.Parse(ReadEntry(nuspecEntry));
            Metadata(nuspec, "id").Should().Be(packageId);
            Metadata(nuspec, "version").Should().Be(expectedVersion);
            Metadata(nuspec, "license").Should().Be(ExpectedLicense);
            Metadata(nuspec, "projectUrl").Should().Be(RepositoryUrl);
            var repository = nuspec.Descendants()
                .Single(element => element.Name.LocalName == "repository");
            repository.Attribute("url")?.Value.Should().Be(RepositoryUrl);
            repository.Attribute("commit")?.Value.Should().Be(expectedCommit);

            var packedLicense = ReadEntry(archive.GetEntry("LICENSE.md")
                ?? throw new AssertionException($"{packageId} does not contain LICENSE.md."));
            packedLicense.Should().Contain("included with SharpClaw.Persistence");
            packedLicense.Should().Contain(RepositoryUrl);
            packedLicense.Should().NotContain("SharpClaw.ProviderIntegrations");

            var module = Modules.SingleOrDefault(candidate => candidate.PackageId == packageId);
            if (module is null)
                continue;

            var manifestPath =
                $"contentFiles/any/net10.0/contributions/{module.SourceId}/package.json";
            var packedManifest = ReadEntry(archive.GetEntry(manifestPath)
                ?? throw new AssertionException($"{packageId} does not contain {manifestPath}."));
            var sourceManifest = File.ReadAllText(
                Path.Combine(sourceRoot, module.PackageId, "package.json"));
            packedManifest.Should().Be(sourceManifest);
            using var manifest = JsonDocument.Parse(packedManifest);
            manifest.RootElement.GetProperty("version").GetString().Should().Be(expectedVersion);
            manifest.RootElement.GetProperty("license").GetString().Should().Be(ExpectedLicense);
            manifest.RootElement.GetProperty("id").GetString().Should().Be(module.SourceId);
        }
    }

    private static string Metadata(XDocument nuspec, string name) =>
        nuspec.Descendants().Single(element => element.Name.LocalName == name).Value;

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static bool IsSafeArchivePath(string path) =>
        !Path.IsPathRooted(path)
        && !path.Split('/', '\\').Contains("..", StringComparer.Ordinal);

    private static string ReadRepositoryVersion(string sourceRoot)
    {
        var props = XDocument.Load(Path.Combine(sourceRoot, "Directory.Build.props"));
        return props.Descendants()
            .Single(element => element.Name.LocalName == "Version")
            .Value;
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

    private sealed record ModuleIdentity(
        string PackageId,
        string SourceId,
        string EntryAssembly,
        string EntryType);
}
