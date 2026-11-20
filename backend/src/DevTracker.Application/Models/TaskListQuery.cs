using DevTracker.Domain.Entities;

namespace DevTracker.Application.Models;

public sealed record TaskListQuery(
    int PageNumber = 1, int PageSize = 20, TaskItemStatus? Status = null, string? Search = null);
