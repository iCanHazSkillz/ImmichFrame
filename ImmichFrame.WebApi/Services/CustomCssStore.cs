namespace ImmichFrame.WebApi.Services;

public class CustomCssStoreOptions
{
    public string? StorePath { get; init; }
    public string? FallbackPath { get; init; }
}

public interface ICustomCssStore
{
    string LoadFallbackCss();
    string? LoadStoredCssOverride();
    string LoadEditableCss();
    string LoadStylesheetContent();
    void Save(string? css);
}

public class CustomCssStore(CustomCssStoreOptions options) : ICustomCssStore
{
    public string? LoadStoredCssOverride()
    {
        var storePath = EnsureStorePathConfigured();
        if (!File.Exists(storePath))
        {
            return null;
        }

        var content = File.ReadAllText(storePath);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    public string LoadEditableCss()
    {
        return LoadStoredCssOverride() ?? LoadFallbackCss();
    }

    public string LoadStylesheetContent()
    {
        return LoadStoredCssOverride() ?? LoadFallbackCss();
    }

    public void Save(string? css)
    {
        var storePath = EnsureStorePathConfigured();
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalized = css?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            if (File.Exists(storePath))
            {
                File.Delete(storePath);
            }

            return;
        }

        WriteAtomically(storePath, normalized);
    }

    private string EnsureStorePathConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.StorePath))
        {
            throw new InvalidOperationException("Custom CSS store path is not configured.");
        }

        return options.StorePath;
    }

    public string LoadFallbackCss()
    {
        var fallbackPath = options.FallbackPath;
        return !string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath)
            ? File.ReadAllText(fallbackPath)
            : string.Empty;
    }

    private static void WriteAtomically(string path, string content)
    {
        var tempPath = Path.Combine(
            Path.GetDirectoryName(path) ?? string.Empty,
            $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                var backupPath = $"{tempPath}.bak";
                try
                {
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                    File.Delete(backupPath);
                }
                catch
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }

                    throw;
                }
            }
            else
            {
                File.Move(tempPath, path, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
