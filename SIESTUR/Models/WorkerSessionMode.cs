namespace Siestur.Models;

/// <summary>
/// Constants for WorkerSession mode values
/// </summary>
public static class WorkerSessionMode
{
    public const string Assigner = "ASSIGNER";
    public const string Window = "WINDOW";

    public static readonly string[] All = { Assigner, Window };

    public static bool IsValid(string mode) => All.Contains(mode);
}
