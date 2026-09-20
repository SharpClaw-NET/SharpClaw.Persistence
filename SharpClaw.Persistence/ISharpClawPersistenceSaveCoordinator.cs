using SharpClaw.Runtime.INF.Persistence;

namespace SharpClaw.Persistence;

public interface ISharpClawPersistenceSaveCoordinator
{
    Task<int> SaveChangesAsync(
        SharpClawDbContext dbContext,
        CancellationToken cancellationToken = default);
}
