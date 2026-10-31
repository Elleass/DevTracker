using DevTracker.Domain.Entities;

namespace DevTracker.Application.Inerfaces
{
    public interface IProjectRepository
    {
        Task<bool> ExistsByNameAsync(
            Guid ownerId,
            string name,
            CancellationToken cancellationToken);
        Task AddAsync(
            Project project,
            CancellationToken cancellationToken);

        Task<List<Project>> GetByOwnerIdAsync(
            Guid ownerId, 
            CancellationToken cancellationToken);

        Task<Project?> GetByIdAsync(
            Guid projectId,
            Guid userId,
            CancellationToken cancellationToken
        );

        Task DeleteAsync(
            Project project,
            CancellationToken cancellationToken
        );

        Task UpdateProject(CancellationToken cancellationToken);

    }
    
}