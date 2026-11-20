namespace DevTracker.Application.Inerfaces;

public interface IProjectWriteLock
{
    Task<IProjectWriteScope> AcquireAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken);
}

public interface IProjectWriteScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
