using DevTracker.Application.Exceptions;
using DevTracker.Domain.Entities;

namespace DevTracker.Application.Services;

public static class DependencyRules
{
    public static void ValidateEdge(Guid taskId, Guid predecessorId, IEnumerable<TaskDependency> links)
    {
        var pending = new Stack<Guid>();
        var seen = new HashSet<Guid>();
        var edges = links.ToLookup(link => link.TaskId, link => link.PredecessorTaskId);
        pending.Push(predecessorId);
        while (pending.TryPop(out var id))
        {
            if (id == taskId) throw new TaskDependencyException("Task dependencies must not contain cycles or self references");
            if (!seen.Add(id)) continue;
            foreach (var next in edges[id]) pending.Push(next);
        }
    }

    public static void ValidateStatus(Guid id, TaskItemStatus status, IReadOnlyCollection<TaskItem> tasks, IReadOnlyCollection<TaskDependency> links)
    {
        var byId = tasks.ToDictionary(task => task.Id);
        if (status == TaskItemStatus.Done && links.Any(link => link.TaskId == id && byId[link.PredecessorTaskId].Status != TaskItemStatus.Done))
            throw new TaskDependencyException("Complete all required predecessors before marking this task Done");
        if (status != TaskItemStatus.Done && links.Any(link => link.PredecessorTaskId == id && byId[link.TaskId].Status == TaskItemStatus.Done))
            throw new TaskDependencyException("Cannot reopen a predecessor of a completed task");
    }
}
