using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DevTracker.Infrastructure.Identity;
using DevTracker.Domain.Entities;
using Microsoft.VisualBasic;
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DevTracker.Infrastructure.Persistence
{

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>,Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = ApplicationRoles.UserRoleId,
                Name = ApplicationRoles.User,
                NormalizedName = ApplicationRoles.User,
                ConcurrencyStamp = "devtracker-user-role-v1"
            },
            new IdentityRole<Guid>
            {
                Id = ApplicationRoles.AdminRoleId,
                Name = ApplicationRoles.Admin,
                NormalizedName = ApplicationRoles.Admin,
                ConcurrencyStamp = "devtracker-admin-role-v1"
            });
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        
        



    }
    

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var projectEntries = ChangeTracker.Entries<Project>();
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in projectEntries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }

            var taskEntries = ChangeTracker.Entries<TaskItem>();
            foreach (var entry in taskEntries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

}
}
