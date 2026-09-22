using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharpClaw.Runtime.INF.Persistence;

namespace SharpClaw.Persistence.SQLite;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SharpClawDbContext>
{
    public SharpClawDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SharpClawDbContext>();
        SQLitePersistenceProvider.ConfigureCanonicalModel(builder);
        var options = builder.UseSqlite(
                "Data Source=sharpclaw_migrations.db",
                provider => provider.MigrationsAssembly(typeof(DesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new SharpClawDbContext(options);
    }
}
