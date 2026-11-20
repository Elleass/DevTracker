using System.ComponentModel;
using DevTracker.Domain.Entities;
using DevTracker.Application.Models;

namespace DevTracker.Application.Inerfaces
{
    public interface ITaskItemRepository
    {
        Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken); 

        Task<List<TaskItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken);
        Task<PagedResult<TaskItem>> GetPageByProjectIdAsync(Guid projectId, TaskListQuery query, CancellationToken cancellationToken);
        Task<TaskItem?> GetByIdAsync(Guid taskItemId, Guid projectId, CancellationToken cancellationToken);
        Task DeleteTask(TaskItem taskItem, CancellationToken cancellationToken);
        Task UpdateTask(CancellationToken cancellationToken);
    }
}
