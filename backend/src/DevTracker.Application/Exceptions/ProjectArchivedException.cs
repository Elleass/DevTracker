namespace DevTracker.Application.Exceptions;

public sealed class ProjectArchivedException : Exception
{
    public ProjectArchivedException() : base("Cannot create or update tasks in an archived project") { }
}
