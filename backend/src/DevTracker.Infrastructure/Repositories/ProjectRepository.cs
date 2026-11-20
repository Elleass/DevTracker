using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevTracker.Infrastructure.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
            private readonly AppDbContext _context;

        public ProjectRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Project project, CancellationToken cancellationToken)
        {
            await _context.Projects.AddAsync(project, cancellationToken);

            await SaveProjectChangesAsync(cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken, Guid? excludedProjectId = null)
        {
           return await _context.Projects.AnyAsync(p => p.OwnerId == ownerId && p.Name == name
               && (!excludedProjectId.HasValue || p.Id != excludedProjectId.Value), cancellationToken);
        }

        public async Task<List<Project>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            return await _context.Projects.AsNoTracking().Where(p => p.OwnerId == ownerId)
                .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).ToListAsync(cancellationToken);
        }


        public async Task<Project?> GetByIdAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
        {
            return await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId, cancellationToken);
        }

        public async Task DeleteAsync(Project project, CancellationToken cancellationToken)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(cancellationToken);
        }


        public async Task UpdateProject(CancellationToken cancellationToken)
        {
            await SaveProjectChangesAsync(cancellationToken);
        }

        private async Task SaveProjectChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Projects_OwnerId_Name"
            })
            {
                throw new ProjectAlreadyExistsException();
            }
        }
    }

}
