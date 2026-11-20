using DevTracker.Api.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DevTracker.Domain.Entities;

namespace DevTracker.Api.DTOs
{
    public class UpdateTaskItemRequest
    {
        [StringLength(150)]
        [NotWhiteSpace]
        public string? Title {get; set;}

        private string? _description;
        [StringLength(4000)]
        public string? Description
        {
            get => _description;
            set { _description = value; DescriptionSpecified = true; }
        }

        [JsonIgnore]
        public bool DescriptionSpecified { get; private set; }

        [EnumDataType(typeof(TaskItemStatus))]
        public TaskItemStatus? Status  {get; set;}

        private DateTimeOffset? _dueAt;
        public DateTimeOffset? DueAt
        {
            get => _dueAt;
            set { _dueAt = value; DueAtSpecified = true; }
        }

        [JsonIgnore]
        public bool DueAtSpecified { get; private set; }


    }
}
