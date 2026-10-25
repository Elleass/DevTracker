

namespace DevTracker.Application.Exceptions
{
    public class ProjectDoNotExistsException : Exception
    {
        public ProjectDoNotExistsException()
            : base("Project do not exists")
        {
        }
    }
}