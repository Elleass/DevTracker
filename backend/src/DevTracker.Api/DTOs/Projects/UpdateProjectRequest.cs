using DevTracker.Domain.Entities;
using DevTracker.Api.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DevTracker.Api.DTOs
{
    public class UpdateProjectRequest
    {
        [StringLength(150)]
        [NotWhiteSpace]
        public string? Name {get; set;}

        private string? _description;
        [StringLength(4000)]
        public string? Description
        {
            get => _description;
            set { _description = value; DescriptionSpecified = true; }
        }

        [JsonIgnore]
        public bool DescriptionSpecified { get; private set; }

        [EnumDataType(typeof(ProjectStatus))]
        public ProjectStatus? ProjectStatus {get;set; }
    }
}
