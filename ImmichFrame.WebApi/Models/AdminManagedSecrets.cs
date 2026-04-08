namespace ImmichFrame.WebApi.Models;

public class AdminManagedSecretsDocument
{
    public string? WeatherApiKey { get; set; }

    public void Normalize()
    {
        WeatherApiKey = string.IsNullOrWhiteSpace(WeatherApiKey)
            ? null
            : WeatherApiKey.Trim();
    }
}
