namespace DevTracker.Application.Exceptions;

public sealed class ProjectHasUnfinishedTasksException : Exception
{
    public ProjectHasUnfinishedTasksException()
        : base("Cannot archive a project with unfinished tasks") { }
}
