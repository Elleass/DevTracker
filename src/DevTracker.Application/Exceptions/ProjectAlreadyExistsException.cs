using System.Data;
using System.Reflection.Metadata.Ecma335;
using DevTracker.Domain.Entities;

namespace DevTracker.Application.Exceptions
{
    public class ProjectAlreadyExistsException : Exception
    {
        public ProjectAlreadyExistsException()
            : base("Project already exists")
        {
        }
    }
}