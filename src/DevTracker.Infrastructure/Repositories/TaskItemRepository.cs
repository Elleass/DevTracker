using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Persistence;

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
                        
                        
            var newTaskItem = await _context.TaskItem.AddAsync(taskItem, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}