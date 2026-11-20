using System.ComponentModel.DataAnnotations;
using DevTracker.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.DTOs;

public sealed class TaskListRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    [FromQuery(Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;

    [EnumDataType(typeof(TaskItemStatus))]
    [FromQuery(Name = "status")]
    public TaskItemStatus? Status { get; set; }

    [StringLength(150)]
    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (((long)PageNumber - 1) * PageSize > int.MaxValue)
        {
            yield return new ValidationResult("The requested page offset is too large.",
                [nameof(PageNumber), nameof(PageSize)]);
        }
    }
}
