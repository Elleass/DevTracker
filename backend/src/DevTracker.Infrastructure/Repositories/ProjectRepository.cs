using System.ComponentModel;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
            var newProject = await _context.Projects.AddAsync(project, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken)
        {
           return await _context.Projects.AnyAsync(p => p.OwnerId == ownerId && p.Name == name, cancellationToken);
        }

        public async Task<List<Project>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            return await _context.Projects.AsNoTracking().Where(p => p.OwnerId == ownerId).ToListAsync();
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
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

}