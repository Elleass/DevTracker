using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

    }
}
