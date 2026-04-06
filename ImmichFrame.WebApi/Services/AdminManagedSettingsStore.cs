using System.Text.Json;
using System.Reflection;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Models;

namespace ImmichFrame.WebApi.Services;

public class AdminManagedSettingsStoreOptions
{
    public string? StorePath { get; init; }
}

public interface IAdminManagedSettingsStore
{
    AdminManagedSettingsDocument LoadOrSeed(ServerSettings bootstrapSettings);
    void Save(AdminManagedSettingsDocument settings);
}

public class AdminManagedSettingsStore(
    AdminManagedSettingsStoreOptions options,
    ILogger<AdminManagedSettingsStore> logger) : IAdminManagedSettingsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public AdminManagedSettingsDocument LoadOrSeed(ServerSettings bootstrapSettings)
    {
        ArgumentNullException.ThrowIfNull(bootstrapSettings);

        var path = EnsurePathConfigured();
        if (!File.Exists(path))
        {
            var seeded = AdminManagedSettingsDocument.FromServerSettings(bootstrapSettings);
            Save(seeded);
            return seeded;
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AdminManagedSettingsDocument>(json, _serializerOptions) ?? new AdminManagedSettingsDocument();
            ApplyCompatibilityDefaults(json, settings, bootstrapSettings);
            settings.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load admin-managed settings from {path}.", path);
            throw;
        }
    }

    public void Save(AdminManagedSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var path = EnsurePathConfigured();
        settings.Normalize();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        WriteAtomically(path, json);
    }

    private string EnsurePathConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.StorePath))
        {
            throw new InvalidOperationException("Admin-managed settings store path is not configured.");
        }

        return options.StorePath;
    }

    private static void ApplyCompatibilityDefaults(
        string json,
        AdminManagedSettingsDocument settings,
        ServerSettings bootstrapSettings)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("General", out var generalElement))
        {
            ApplyMissingGeneralSettings(generalElement, settings.General, bootstrapSettings.GeneralSettings);
        }

        if (!document.RootElement.TryGetProperty("Accounts", out var accountsElement) || accountsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var bootstrapAccounts = bootstrapSettings.Accounts.ToList();
        for (var index = 0; index < settings.Accounts.Count && index < bootstrapAccounts.Count; index++)
        {
            if (index >= accountsElement.GetArrayLength())
            {
                break;
            }

            var accountElement = accountsElement[index];
            if (!accountElement.TryGetProperty("AccountIdentifier", out _))
            {
                settings.Accounts[index].AccountIdentifier = ServerSettingsFactory.GetAccountIdentifier(bootstrapAccounts[index]);
            }
        }
    }

    private static void ApplyMissingGeneralSettings(
        JsonElement generalElement,
        AdminManagedGeneralSettings managedSettings,
        IGeneralSettings bootstrapSettings)
    {
        foreach (var managedProperty in typeof(AdminManagedGeneralSettings).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!managedProperty.CanWrite || generalElement.TryGetProperty(managedProperty.Name, out _))
            {
                continue;
            }

            var bootstrapProperty = bootstrapSettings.GetType().GetProperty(managedProperty.Name, BindingFlags.Instance | BindingFlags.Public);
            if (bootstrapProperty is null || !bootstrapProperty.CanRead)
            {
                continue;
            }

            var bootstrapValue = bootstrapProperty.GetValue(bootstrapSettings);
            managedProperty.SetValue(managedSettings, CloneGeneralSettingValue(bootstrapValue, managedProperty.PropertyType));
        }
    }

    private static object? CloneGeneralSettingValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (value is List<string> strings && targetType == typeof(List<string>))
        {
            return strings.ToList();
        }

        if (value is IEnumerable<string> enumerableStrings && targetType == typeof(List<string>))
        {
            return enumerableStrings.ToList();
        }

        return value;
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
