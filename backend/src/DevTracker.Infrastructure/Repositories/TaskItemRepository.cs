using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DevTracker.Application.Models;

namespace DevTracker.Infrastructure.Repositories
{
    public class TaskItemRepository : ITaskItemRepository
    {
        private readonly AppDbContext _context;

        public TaskItemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken)
        {
            await _context.TaskItems.AddAsync(taskItem, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteTask(TaskItem taskItem, CancellationToken cancellationToken)
        {
            _context.TaskItems.Remove(taskItem);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<TaskItem?> GetByIdAsync(Guid taskItemId, Guid projectId, CancellationToken cancellationToken)
        {
            return await _context.TaskItems.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == taskItemId, cancellationToken);
        }

        public async Task<List<TaskItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return await _context.TaskItems.AsNoTracking().Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).ToListAsync(cancellationToken);
        }

        public async Task UpdateTask(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PagedResult<TaskItem>> GetPageByProjectIdAsync(Guid projectId,
            TaskListQuery query, CancellationToken cancellationToken)
        {
            var tasks = _context.TaskItems.AsNoTracking().Where(task => task.ProjectId == projectId);
            if (query.Status is not null)
            {
                tasks = tasks.Where(task => task.Status == query.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                tasks = tasks.Where(task => EF.Functions.ILike(task.Title, $"%{search}%", "\\"));
            }

            var totalCount = await tasks.CountAsync(cancellationToken);
            var items = await tasks.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id)
                .Skip(checked((query.PageNumber - 1) * query.PageSize)).Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<TaskItem>(items, totalCount, query.PageNumber, query.PageSize);
        }
    }
}
