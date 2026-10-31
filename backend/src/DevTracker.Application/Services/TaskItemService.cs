using System.ComponentModel;
using System.Net.Http.Headers;
using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;

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
        public async Task<Guid> AddTaskItemAsync(Guid projectId, Guid ownerId, string title, string? description, CancellationToken cancellationToken)
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
                ProjectId = projectId
                
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

        public Task<TaskItem> GetTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<List<TaskItem>> GetTaskItemsAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken );
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }

            var tasksItem = await _taskItemRepository.GetByProjectIdAsync(projectId, cancellationToken);
            return tasksItem;

        }

        public async Task UpdateTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, string? title, string? description, TaskItemStatus? status, CancellationToken cancellationToken)
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
            if (description is not null)
            {
                task.Description = description;
            }
            if (status is not null)
            {
                task.Status = status.Value;
            }
    
            await _taskItemRepository.UpdateTask(cancellationToken);
        }
    }
}