using DevTracker.Domain.Entities;

namespace DevTracker.Application.Inerfaces
{
    public interface ITaskItemRepository
    {
        Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken); 
    }
}