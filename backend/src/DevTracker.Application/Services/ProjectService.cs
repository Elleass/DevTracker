using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;

namespace DevTracker.Application.Services
{
    public class ProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ITaskItemRepository _taskItemRepository;
        private readonly TimeProvider _timeProvider;
        private readonly IProjectWriteLock _writeLock;

        public ProjectService(IProjectRepository projectRepository,
            ITaskItemRepository taskItemRepository, TimeProvider timeProvider, IProjectWriteLock writeLock)
        {
            _projectRepository = projectRepository;
            _taskItemRepository = taskItemRepository;
            _timeProvider = timeProvider;
            _writeLock = writeLock;
        }

        public async Task<Guid> CreateProjectAsync(Guid userId, string name, string? description, CancellationToken cancellationToken)
        {
            //Name should be unique per user
            if (await _projectRepository.ExistsByNameAsync(userId, name, cancellationToken))
            {
                throw new ProjectAlreadyExistsException();
            }

            Project project = new()
            {
                OwnerId = userId,
                Name = name,
                Description = description
            };

            await _projectRepository.AddAsync(project, cancellationToken);

            return project.Id;
        }

        public async Task<List<Project>> GetProjectsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _projectRepository.GetByOwnerIdAsync(userId, cancellationToken);
        }

        public async Task<Project> GetProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, userId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
            return project;
        }

        public async Task DeleteProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
        {
            await using var scope = await _writeLock.AcquireAsync(projectId, userId, cancellationToken);
            var project = await _projectRepository.GetByIdAsync(projectId, userId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
            await _projectRepository.DeleteAsync(project, cancellationToken);
            await scope.CommitAsync(cancellationToken);
        }

        public async Task ArchiveProjectAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken)
        {
            await using var scope = await _writeLock.AcquireAsync(projectId, ownerId, cancellationToken);
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken)
                ?? throw new ProjectDoNotExistsException();

            if (project.ArchivedAt is not null) return;

            var tasks = await _taskItemRepository.GetByProjectIdAsync(projectId, cancellationToken);
            if (tasks.Any(task => task.Status != TaskItemStatus.Done))
            {
                throw new ProjectHasUnfinishedTasksException();
            }

            project.ArchivedAt = _timeProvider.GetUtcNow();
            await _projectRepository.UpdateProject(cancellationToken);
            await scope.CommitAsync(cancellationToken);
        }


        public async Task<Project> UpdateProjectAsync(
            Guid projectId,
            Guid ownerId,
            string? name,
            string? description,
            bool descriptionSpecified,
            ProjectStatus? status,
            CancellationToken cancellationToken)
        {
            await using var scope = await _writeLock.AcquireAsync(projectId, ownerId, cancellationToken);
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
            if (name is not null)
            {
                if (await _projectRepository.ExistsByNameAsync(ownerId, name, cancellationToken, projectId))
                {
                    throw new ProjectAlreadyExistsException();
                }
                project.Name = name;
            }
            if (descriptionSpecified)
            {
                project.Description = description;
            }
            if (status is not null)
            {
                project.Status = status.Value;
            }
            await _projectRepository.UpdateProject(cancellationToken);
            await scope.CommitAsync(cancellationToken);
            return project;
        }
    }
}
