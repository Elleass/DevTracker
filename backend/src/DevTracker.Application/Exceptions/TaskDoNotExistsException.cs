

namespace DevTracker.Application.Exceptions
{
    public class TaskDoNotExistsException : Exception
    {
        public TaskDoNotExistsException()
            : base("Task do not exists")
        {
        }
    }
}