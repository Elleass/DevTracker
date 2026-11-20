using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Application.Services;
using DevTracker.Domain.Entities;
using DevTracker.Application.Models;

namespace DevTracker.Tests;

public class ArchiveProjectTests
{
    [Theory]
    [InlineData(TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress)]
    public async Task Unfinished_task_blocks_archiving(TaskItemStatus status)
    {
        var projects = new ProjectRepositoryStub();
        var tasks = new TaskRepositoryStub(status);
        var service = new ProjectService(projects, tasks, TimeProvider.System, new WriteLockStub());

        await Assert.ThrowsAsync<ProjectHasUnfinishedTasksException>(() => service.ArchiveProjectAsync(
            projects.Project.Id, projects.Project.OwnerId, CancellationToken.None));

        Assert.Null(projects.Project.ArchivedAt);
        Assert.False(projects.Saved);
    }

    [Fact]
    public async Task Empty_project_can_be_archived()
    {
        var projects = new ProjectRepositoryStub();
        var service = new ProjectService(projects, new TaskRepositoryStub(), TimeProvider.System, new WriteLockStub());

        await service.ArchiveProjectAsync(projects.Project.Id, projects.Project.OwnerId, CancellationToken.None);

        Assert.NotNull(projects.Project.ArchivedAt);
        Assert.True(projects.Saved);
    }

    [Fact]
    public async Task Project_with_only_done_tasks_can_be_archived()
    {
        var projects = new ProjectRepositoryStub();
        var service = new ProjectService(projects,
            new TaskRepositoryStub(TaskItemStatus.Done, TaskItemStatus.Done), TimeProvider.System, new WriteLockStub());

        await service.ArchiveProjectAsync(projects.Project.Id, projects.Project.OwnerId, CancellationToken.None);

        Assert.NotNull(projects.Project.ArchivedAt);
        Assert.True(projects.Saved);
    }

    [Fact]
    public async Task One_unfinished_task_among_done_tasks_still_blocks_archiving()
    {
        var projects = new ProjectRepositoryStub();
        var service = new ProjectService(projects,
            new TaskRepositoryStub(TaskItemStatus.Done, TaskItemStatus.ToDo), TimeProvider.System, new WriteLockStub());

        await Assert.ThrowsAsync<ProjectHasUnfinishedTasksException>(() => service.ArchiveProjectAsync(
            projects.Project.Id, projects.Project.OwnerId, CancellationToken.None));
        Assert.False(projects.Saved);
    }

    [Fact]
    public async Task Another_owner_cannot_archive_or_read_project_tasks()
    {
        var projects = new ProjectRepositoryStub();
        var tasks = new TaskRepositoryStub();
        var service = new ProjectService(projects, tasks, TimeProvider.System, new WriteLockStub());

        await Assert.ThrowsAsync<ProjectDoNotExistsException>(() => service.ArchiveProjectAsync(
            projects.Project.Id, Guid.NewGuid(), CancellationToken.None));

        Assert.False(tasks.Read);
        Assert.False(projects.Saved);
    }

    [Fact]
    public async Task Missing_project_cannot_be_archived()
    {
        var projects = new ProjectRepositoryStub();
        var tasks = new TaskRepositoryStub();
        var service = new ProjectService(projects, tasks, TimeProvider.System, new WriteLockStub());

        await Assert.ThrowsAsync<ProjectDoNotExistsException>(() => service.ArchiveProjectAsync(
            Guid.NewGuid(), projects.Project.OwnerId, CancellationToken.None));

        Assert.False(tasks.Read);
        Assert.False(projects.Saved);
    }

    [Fact]
    public async Task Repeated_archive_preserves_original_timestamp()
    {
        var projects = new ProjectRepositoryStub();
        var original = DateTimeOffset.UtcNow.AddDays(-1);
        projects.Project.ArchivedAt = original;
        var tasks = new TaskRepositoryStub();
        var service = new ProjectService(projects, tasks, TimeProvider.System, new WriteLockStub());

        await service.ArchiveProjectAsync(projects.Project.Id, projects.Project.OwnerId, CancellationToken.None);

        Assert.Equal(original, projects.Project.ArchivedAt);
        Assert.False(tasks.Read);
        Assert.False(projects.Saved);
    }

    private sealed class WriteLockStub : IProjectWriteLock, IProjectWriteScope
    {
        public Task<IProjectWriteScope> AcquireAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken)
            => Task.FromResult<IProjectWriteScope>(this);
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public Project Project { get; } = new() { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Name = "Unit test" };
        public bool Saved { get; private set; }

        public Task<Project?> GetByIdAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(projectId == Project.Id && userId == Project.OwnerId ? Project : null);

        public Task UpdateProject(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken, Guid? excludedProjectId = null) => throw new NotSupportedException();
        public Task AddAsync(Project project, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<Project>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Project project, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TaskRepositoryStub(params TaskItemStatus[] statuses) : ITaskItemRepository
    {
        public bool Read { get; private set; }

        public Task<List<TaskItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken)
        {
            Read = true;
            return Task.FromResult(statuses.Select(status => new TaskItem
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Title = "Unit test", Status = status
            }).ToList());
        }

        public Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResult<TaskItem>> GetPageByProjectIdAsync(Guid projectId, TaskListQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TaskItem?> GetByIdAsync(Guid taskItemId, Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteTask(TaskItem taskItem, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateTask(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
