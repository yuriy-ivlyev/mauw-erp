namespace Mauw.Erp.Application.Settings;

public class GooglePlacesSettings
{
    public const string Section = "GooglePlaces";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://places.googleapis.com/v1/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
