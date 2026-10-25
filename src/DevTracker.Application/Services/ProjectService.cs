using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;

namespace DevTracker.Application.Services
{
    public class ProjectService
    {
        private readonly IProjectRepository _projectRepository;

        public ProjectService(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
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
            var project = await _projectRepository.GetByIdAsync(projectId, userId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
            await _projectRepository.DeleteAsync(project, cancellationToken);
        }


        public async Task UpdateProjectAsync(
            Guid projectId,
            Guid ownerId,
            string? name,
            string? description,
            CancellationToken cancellationToken)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, ownerId, cancellationToken);
            if (project == null)
            {
                throw new ProjectDoNotExistsException();
            }
            if (name is not null)
            {
                project.Name = name;
            }
            if (description is not null)
            {
                project.Description = description;
            }
            await _projectRepository.UpdateProject(cancellationToken);
        }
    }
}