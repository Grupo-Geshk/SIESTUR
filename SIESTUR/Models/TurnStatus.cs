namespace Siestur.Models;

/// <summary>
/// Constants for Turn status values to avoid magic strings throughout the codebase
/// </summary>
public static class TurnStatus
{
    public const string Pending = "PENDING";
    public const string Called = "CALLED";
    public const string Serving = "SERVING";
    public const string Done = "DONE";
    public const string Skipped = "SKIPPED";

    public static readonly string[] All = { Pending, Called, Serving, Done, Skipped };

    public static bool IsValid(string status) => All.Contains(status);
}
