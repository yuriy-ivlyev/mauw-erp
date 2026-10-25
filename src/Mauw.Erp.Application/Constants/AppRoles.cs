namespace Mauw.Erp.Application.Constants;

/// <summary>Application roles; values must match the app roles defined in Microsoft Entra ID.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Sales = "Sales";
    public const string Warehouse = "Warehouse";
    public const string Accounting = "Accounting";

    public static readonly IReadOnlyList<string> All = [Admin, Sales, Warehouse, Accounting];
}
