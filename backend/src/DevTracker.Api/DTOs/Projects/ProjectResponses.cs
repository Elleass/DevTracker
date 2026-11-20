using DevTracker.Domain.Entities;

namespace DevTracker.Api.DTOs;

public sealed record CreatedProjectResponse(Guid ProjectId);
public sealed record ProjectListResponse(List<Project> Projects);
public sealed record ProjectDetailsResponse(Project Project);
public sealed record UpdatedProjectResponse(
    Guid ProjectId, string Name, string? Description, ProjectStatus Status);
