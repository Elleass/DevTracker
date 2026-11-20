using DevTracker.Domain.Entities;
using DevTracker.Application.Models;

namespace DevTracker.Application.Inerfaces
{
    public interface ITaskItemService
    {
        Task<TaskItem> GetTaskItemAsync(
            Guid projectId, 
            Guid taskItemId, 
            Guid ownerId, 
            CancellationToken cancellationToken);
        Task<PagedResult<TaskItem>> GetTaskItemsAsync(
            Guid projectId, 
            Guid ownerId, 
            TaskListQuery query,
            CancellationToken cancellationToken);
        Task<Guid> AddTaskItemAsync(
            Guid projectId,
            Guid ownerId,
            string title,
            string? description,
            DateTimeOffset? dueAt,
            CancellationToken cancellationToken, int? estimatedMinutes = null, TaskPriority priority = TaskPriority.Normal);
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
            TaskItemStatus? status,
            bool descriptionSpecified,
            DateTimeOffset? dueAt,
            bool dueAtSpecified,
            CancellationToken cancellationToken, int? estimatedMinutes = null, bool estimatedMinutesSpecified = false, TaskPriority? priority = null);

    }
}
