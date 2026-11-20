using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public NotWhiteSpaceAttribute() : base("{0} cannot be empty or contain only whitespace.") { }

    public override bool IsValid(object? value) =>
        value is null || value is string text && !string.IsNullOrWhiteSpace(text);
}
