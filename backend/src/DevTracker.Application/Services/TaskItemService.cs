using System.ComponentModel;
using System.Net.Http.Headers;
using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Application.Models;

namespace DevTracker.Application.Services
{

    public class TaskItemService : ITaskItemService
    {
        private readonly IProjectRepository _projectRepository;



        private readonly ITaskItemRepository _taskItemRepository;
        private readonly IProjectWriteLock _writeLock;
        private readonly IWorkspaceRepository _workspace;

        public TaskItemService(IProjectRepository projectRepository, ITaskItemRepository taskItemRepository, IProjectWriteLock writeLock, IWorkspaceRepository workspace)
        {
            _projectRepository = projectRepository;
            _taskItemRepository = taskItemRepository;
            _writeLock = writeLock;
            _workspace = workspace;
        }
        public async Task<Guid> AddTaskItemAsync(Guid projectId, Guid ownerId, string title, string? description, DateTimeOffset? dueAt, CancellationToken cancellationToken, int? estimatedMinutes = null, TaskPriority priority = TaskPriority.Normal)
        {
            await using var scope = await _writeLock.AcquireAsync(projectId, ownerId, cancellationToken);
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project == null)
            {
                 throw new ProjectDoNotExistsException();
            }
            if (project.ArchivedAt is not null) throw new ProjectArchivedException();

            TaskItem taskItem = new TaskItem
            {
                Title = title,
                Description = description,
                ProjectId = projectId,
                DueAt = dueAt?.ToUniversalTime(),
                EstimatedMinutes = estimatedMinutes,
                Priority = priority
                
            };
            await _taskItemRepository.AddAsync(taskItem, cancellationToken );
            await scope.CommitAsync(cancellationToken);

            return(taskItem.Id);

        }

        public async Task DeleteTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, CancellationToken cancellationToken)
        {
            await using var scope = await _writeLock.AcquireAsync(projectId, ownerId, cancellationToken);
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken );
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            } 

            var task = await _taskItemRepository.GetByIdAsync(taskItemId, projectId, cancellationToken);
            if (task == null)
            {
                throw new TaskDoNotExistsException();
            }
            if ((await _workspace.DependenciesAsync(projectId, cancellationToken)).Any(link => link.PredecessorTaskId == taskItemId))
                throw new TaskDependencyException("Remove dependent task links before deleting this predecessor");
            await _taskItemRepository.DeleteTask(task, cancellationToken);
            await scope.CommitAsync(cancellationToken);
        }

        public async Task<TaskItem> GetTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project is null)
            {
                throw new ProjectDoNotExistsException();
            }

            return await _taskItemRepository.GetByIdAsync(taskItemId, projectId, cancellationToken)
                ?? throw new TaskDoNotExistsException();
        }

        public async Task<PagedResult<TaskItem>> GetTaskItemsAsync(Guid projectId, Guid ownerId,
            TaskListQuery query, CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken );
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }

            var tasksItem = await _taskItemRepository.GetPageByProjectIdAsync(projectId, query, cancellationToken);
            return tasksItem;

        }

        public async Task UpdateTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId,
            string? title, string? description, TaskItemStatus? status,
            bool descriptionSpecified, DateTimeOffset? dueAt, bool dueAtSpecified,
            CancellationToken cancellationToken, int? estimatedMinutes = null, bool estimatedMinutesSpecified = false, TaskPriority? priority = null)
        {
            await using var scope = await _writeLock.AcquireAsync(projectId, ownerId, cancellationToken);
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
            if (project.ArchivedAt is not null) throw new ProjectArchivedException();
            var task = await _taskItemRepository.GetByIdAsync(taskItemId, projectId, cancellationToken);
            if (task == null)
            {
                throw new TaskDoNotExistsException();
            }
            if (title is not null)
            {
                task.Title = title;
            }
            if (descriptionSpecified)
            {
                task.Description = description;
            }
            if (status is not null)
            {
                DependencyRules.ValidateStatus(taskItemId, status.Value,
                    await _taskItemRepository.GetByProjectIdAsync(projectId, cancellationToken),
                    await _workspace.DependenciesAsync(projectId, cancellationToken));
                task.Status = status.Value;
            }
            if (dueAtSpecified)
            {
                task.DueAt = dueAt?.ToUniversalTime();
            }
            if (estimatedMinutesSpecified) task.EstimatedMinutes = estimatedMinutes;
            if (priority is not null) task.Priority = priority.Value;
    
            await _taskItemRepository.UpdateTask(cancellationToken);
            await scope.CommitAsync(cancellationToken);
        }
    }
}
