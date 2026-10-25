using DevTracker.Domain.Entities;

namespace DevTracker.Application.Inerfaces
{
    public interface ITaskItemService
    {
        Task<TaskItem> GetTaskItemAsync(
            Guid projectId, 
            Guid taskItemId, 
            Guid ownerId, 
            CancellationToken cancellationToken);
        Task<List<TaskItem>> GetTasksItemAsync(
            Guid projectId, 
            Guid ownerId, 
            CancellationToken cancellationToken);
        Task<Guid> AddTaskItemAsync(
            Guid projectId,
            Guid ownerId,
            string title,
            string? description,
            CancellationToken cancellationToken);
        Task DeleteTaskItemAsync(
            Guid projectId,
            Guid taskItemId, 
            Guid ownerId, 
            CancellationToken cancellationToken);
        Task UpdateTaskItemAsync(
            Guid projectId, 
            Guid taskItemId,
            Guid ownerId,
            string? title,
            string? description, 
            TaskItemStatus status,
            CancellationToken cancellationToken);

    }
}
