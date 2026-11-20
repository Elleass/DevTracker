using System.Data;
using DevTracker.Application.Inerfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DevTracker.Infrastructure.Persistence;

public sealed class PostgreSqlProjectWriteLock(AppDbContext context) : IProjectWriteLock
{
    public async Task<IProjectWriteScope> AcquireAsync(Guid projectId, Guid ownerId,
        CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // All project/task writes take this same database row lock before reading state.
            await context.Projects.FromSqlInterpolated($"""
                SELECT * FROM "Projects"
                WHERE "Id" = {projectId} AND "OwnerId" = {ownerId}
                FOR UPDATE
                """).AsNoTracking().ToListAsync(cancellationToken);
            return new WriteScope(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class WriteScope(IDbContextTransaction transaction) : IProjectWriteScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
