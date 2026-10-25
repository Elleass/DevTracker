namespace DevTracker.Domain.Entities
{
    public class Project
    {
        public Guid Id {get; set;}
        public Guid OwnerId {get; set;}



        public string Name {get; set;} = string.Empty;
        public string? Description {get; set;}
        public ProjectStatus Status {get; set;} = ProjectStatus.Idea;
        public DateTimeOffset? ArchivedAt {get; set;}
        public DateTimeOffset CreatedAt {get; set;}
        public DateTimeOffset UpdatedAt {get; set;}
    }


    public enum ProjectStatus
    {
        Idea,
        Active,
        OnHold,
        Completed

    }

}