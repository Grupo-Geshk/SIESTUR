namespace Siestur.Services;

/// <summary>
/// Abstraction for DateTime to enable testability
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
