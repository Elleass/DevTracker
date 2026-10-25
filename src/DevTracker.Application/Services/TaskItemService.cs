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

        public Task DeleteTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TaskItem> GetTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<TaskItem>> GetTasksItemAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateTaskItemAsync(Guid projectId, Guid taskItemId, Guid ownerId, string? title, string? description, TaskItemStatus status, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}