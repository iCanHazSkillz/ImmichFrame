namespace ImmichFrame.WebApi.Services;

public class CustomCssStoreOptions
{
    public string? StorePath { get; init; }
    public string? FallbackPath { get; init; }
}

public interface ICustomCssStore
{
    string LoadEditableCss();
    string LoadStylesheetContent();
    void Save(string? css);
}

public class CustomCssStore(CustomCssStoreOptions options) : ICustomCssStore
{
    public string LoadEditableCss()
    {
        var storePath = EnsureStorePathConfigured();
        return File.Exists(storePath) ? File.ReadAllText(storePath) : string.Empty;
    }

    public string LoadStylesheetContent()
    {
        var storePath = EnsureStorePathConfigured();
        if (File.Exists(storePath))
        {
            return File.ReadAllText(storePath);
        }

        var fallbackPath = options.FallbackPath;
        return !string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath)
            ? File.ReadAllText(fallbackPath)
            : string.Empty;
    }

    public void Save(string? css)
    {
        var storePath = EnsureStorePathConfigured();
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(storePath, css ?? string.Empty);
    }

    private string EnsureStorePathConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.StorePath))
        {
            throw new InvalidOperationException("Custom CSS store path is not configured.");
        }

        return options.StorePath;
    }
}
