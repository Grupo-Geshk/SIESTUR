namespace Siestur.Models;

/// <summary>
/// Constants for User role values
/// </summary>
public static class UserRole
{
    public const string Admin = "Admin";
    public const string Colaborador = "Colaborador";

    public static readonly string[] All = { Admin, Colaborador };

    public static bool IsValid(string role) => All.Contains(role);
}
