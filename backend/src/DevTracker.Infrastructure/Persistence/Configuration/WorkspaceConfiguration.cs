using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configuration;

public sealed class WorkSessionConfiguration : IEntityTypeConfiguration<WorkSession>
{
    public void Configure(EntityTypeBuilder<WorkSession> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("IX_WorkSessions_ActiveUser");
        b.HasIndex(x => new { x.UserId, x.StartedAt, x.Id });
        b.HasIndex(x => new { x.ProjectId, x.StartedAt });
        b.ToTable("WorkSessions", table => table.HasCheckConstraint("CK_WorkSession_Time", "\"EndedAt\" IS NULL OR \"EndedAt\" >= \"StartedAt\""));
        b.Ignore(x => x.DurationSeconds);
    }
}
public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.ContentMarkdown).HasMaxLength(100000).IsRequired();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.UpdatedAt });
    }
}
public sealed class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> b)
    {
        b.HasKey(x => new { x.TaskId, x.PredecessorTaskId });
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.PredecessorTaskId).OnDelete(DeleteBehavior.Cascade);
        b.ToTable("TaskDependencies", table => table.HasCheckConstraint("CK_Dependency_NotSelf", "\"TaskId\" <> \"PredecessorTaskId\""));
    }
}
