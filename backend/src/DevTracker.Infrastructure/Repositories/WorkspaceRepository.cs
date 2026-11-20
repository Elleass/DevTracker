using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Application.Models;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevTracker.Infrastructure.Repositories;

public sealed class WorkspaceRepository(AppDbContext db) : IWorkspaceRepository
{
    public Task<WorkSession?> ActiveSessionAsync(Guid userId, CancellationToken ct) =>
        db.WorkSessions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.EndedAt == null, ct);
    public Task<WorkSession?> SessionAsync(Guid id, Guid userId, CancellationToken ct) =>
        db.WorkSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    public async Task SaveSessionAsync(WorkSession session, bool isNew, CancellationToken ct)
    {
        if (isNew) db.WorkSessions.Add(session); else db.WorkSessions.Update(session);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_WorkSessions_ActiveUser" })
        { throw new ActiveWorkSessionException(); }
    }
    public async Task<PagedResult<WorkSession>> SessionsAsync(Guid userId, Guid? projectId, int page, int size, CancellationToken ct)
    {
        var query = db.WorkSessions.AsNoTracking().Where(x => x.UserId == userId);
        if (projectId is { } id) query = query.Where(x => x.ProjectId == id);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id)
            .Skip(checked((page - 1) * size)).Take(size).ToListAsync(ct);
        return new(items, count, page, size);
    }
    public Task<List<Document>> DocumentsAsync(Guid projectId, CancellationToken ct) =>
        db.Documents.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<Document?> DocumentAsync(Guid id, Guid projectId, CancellationToken ct) =>
        db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct);
    public async Task SaveDocumentAsync(Document document, bool isNew, CancellationToken ct)
    {
        if (isNew) db.Documents.Add(document); else db.Documents.Update(document);
        await db.SaveChangesAsync(ct);
    }
    public async Task DeleteDocumentAsync(Document document, CancellationToken ct)
    {
        db.Documents.Remove(document); await db.SaveChangesAsync(ct);
    }
    public Task<List<TaskDependency>> DependenciesAsync(Guid projectId, CancellationToken ct) =>
        db.TaskDependencies.AsNoTracking().Where(link => db.TaskItems.Any(task => task.Id == link.TaskId && task.ProjectId == projectId)).ToListAsync(ct);
    public async Task AddDependencyAsync(TaskDependency dependency, CancellationToken ct)
    {
        db.TaskDependencies.Add(dependency); await db.SaveChangesAsync(ct);
    }
    public async Task DeleteDependencyAsync(TaskDependency dependency, CancellationToken ct)
    {
        db.TaskDependencies.Remove(dependency); await db.SaveChangesAsync(ct);
    }
    public async Task RebuildStatisticsAsync(Guid? projectId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (projectId is { } id)
            await db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        else
            await db.Projects.FromSqlRaw("SELECT * FROM \"Projects\" ORDER BY \"Id\" FOR UPDATE").AsNoTracking().ToListAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Projects" p SET "TotalWorkedSeconds" = (
                SELECT COALESCE(SUM(FLOOR(EXTRACT(EPOCH FROM (w."EndedAt" - w."StartedAt")))), 0)::bigint
                FROM "WorkSessions" w WHERE w."ProjectId" = p."Id" AND w."EndedAt" IS NOT NULL)
            WHERE ({projectId}::uuid IS NULL OR p."Id" = {projectId})
            """, ct);
        await transaction.CommitAsync(ct);
    }
}
