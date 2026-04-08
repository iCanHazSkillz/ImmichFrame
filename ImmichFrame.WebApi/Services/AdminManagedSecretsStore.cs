using System.Text.Json;
using ImmichFrame.WebApi.Models;

namespace ImmichFrame.WebApi.Services;

public class AdminManagedSecretsStoreOptions
{
    public string? StorePath { get; init; }
}

public interface IAdminManagedSecretsStore
{
    AdminManagedSecretsDocument Load();
    void Save(AdminManagedSecretsDocument secrets);
}

public class AdminManagedSecretsStore(AdminManagedSecretsStoreOptions options) : IAdminManagedSecretsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public AdminManagedSecretsDocument Load()
    {
        var path = EnsurePathConfigured();
        if (!File.Exists(path))
        {
            return new AdminManagedSecretsDocument();
        }

        var json = File.ReadAllText(path);
        AdminManagedSecretsDocument secrets;
        try
        {
            secrets = JsonSerializer.Deserialize<AdminManagedSecretsDocument>(json, _serializerOptions)
                ?? new AdminManagedSecretsDocument();
        }
        catch (JsonException)
        {
            return new AdminManagedSecretsDocument();
        }

        secrets.Normalize();
        return secrets;
    }

    public void Save(AdminManagedSecretsDocument secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        var path = EnsurePathConfigured();
        secrets.Normalize();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (string.IsNullOrWhiteSpace(secrets.WeatherApiKey))
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        var json = JsonSerializer.Serialize(secrets, _serializerOptions);
        WriteAtomically(path, json);
    }

    private string EnsurePathConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.StorePath))
        {
            throw new InvalidOperationException("Admin-managed secrets store path is not configured.");
        }

        return options.StorePath;
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
