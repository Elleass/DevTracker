using System.Data;
using System.Reflection.Metadata.Ecma335;
using DevTracker.Domain.Entities;

namespace DevTracker.Application.Exceptions
{
    public class FailedToUpdateProjectException : Exception
    {
        public FailedToUpdateProjectException()
            : base("Failed to update project")
        {
        }
    }
}