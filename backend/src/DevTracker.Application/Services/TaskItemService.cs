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

        public TaskItemService(IProjectRepository projectRepository, ITaskItemRepository taskItemRepository)
        {
            _projectRepository = projectRepository;
            _taskItemRepository = taskItemRepository;
        }
        public async Task<Guid> AddTaskItemAsync(Guid projectId, Guid ownerId, string title, string? description, DateTimeOffset? dueAt, CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project == null)
            {
                 throw new ProjectDoNotExistsException();
            }

            TaskItem taskItem = new TaskItem
            {
                Title = title,
                Description = description,
                ProjectId = projectId,
                DueAt = dueAt?.ToUniversalTime()
                
            };
            await _taskItemRepository.AddAsync(taskItem, cancellationToken );

            return(taskItem.Id);

        }

        public async Task DeleteTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, CancellationToken cancellationToken)
        {
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
            await _taskItemRepository.DeleteTask(task, cancellationToken);
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
            CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
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
                task.Status = status.Value;
            }
            if (dueAtSpecified)
            {
                task.DueAt = dueAt?.ToUniversalTime();
            }
    
            await _taskItemRepository.UpdateTask(cancellationToken);
        }
    }
}
