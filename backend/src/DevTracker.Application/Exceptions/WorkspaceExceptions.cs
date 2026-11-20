namespace DevTracker.Application.Exceptions;

public sealed class ActiveWorkSessionException() : Exception("You already have an active work session");
public sealed class ArchivedWorkSessionException() : Exception("Cannot start work in an archived project");
public sealed class WorkSessionNotFoundException() : Exception("Work session not found");
public sealed class DocumentNotFoundException() : Exception("Document not found");
public sealed class TaskDependencyException(string message) : Exception(message);
