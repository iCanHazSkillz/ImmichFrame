namespace ImmichFrame.WebApi.Services;

public sealed class AppInstanceTokenProvider
{
    public const string ResponseHeaderName = "X-ImmichFrame-Instance";

    public string CurrentToken { get; }

    public AppInstanceTokenProvider(string? applicationVersion = null)
    {
        var version = string.IsNullOrWhiteSpace(applicationVersion) ? "unknown" : applicationVersion.Trim();
        CurrentToken = $"{version}-{Guid.NewGuid():N}";
    }
}
