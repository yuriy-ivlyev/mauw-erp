namespace Mauw.Erp.Application.Constants;

/// <summary>
/// Application constants for branding, logos, and asset paths.
/// Used for reports, exports, and UI generation.
/// </summary>
public static class AppConstants
{
    public const string AppName = "Mauw ERP";
    public const string CompanyName = "Mauw";
    
    public static class Assets
    {
        public const string LogoPath = "/api/v1/settings/company/logos/horizontal";
        public const string LogoWidth = "200px";
        public const string LogoHeight = "auto";
    }
    
    public static class Branding
    {
        public const string PrimaryColor = "#915F6D";
        public const string SecondaryColor = "#FFFFFF";
        public const string FontFamily = "Arial, sans-serif";
    }
}
