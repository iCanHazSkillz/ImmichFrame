using ImmichFrame.WebApi.Models;
using ImmichFrame.Core.Interfaces;
using System.Text.Json;

namespace ImmichFrame.WebApi.Services;

public sealed class BootstrapServerSettingsHolder
{
    public BootstrapServerSettingsHolder(ServerSettings settings)
    {
        Settings = ServerSettingsFactory.Clone(settings);
        ServerSettingsFactory.EnsureAccountIdentifiers(Settings);
    }

    public ServerSettings Settings { get; }
}

public sealed record EffectiveSettingsSnapshot(long Version, ServerSettings Settings, string CustomCss, bool HasCustomCssOverride);

public interface IWritableEffectiveSettingsProvider : ISettingsSnapshotProvider
{
    new EffectiveSettingsSnapshot GetCurrentSnapshot();
    EffectiveSettingsSnapshot Update(AdminManagedSettingsDocument settings, string? customCss);
}

public sealed class EffectiveSettingsProvider : IWritableEffectiveSettingsProvider
{
    private readonly object _sync = new();
    private readonly ServerSettings _bootstrapSettings;
    private readonly IAdminManagedSettingsStore _store;
    private readonly ICustomCssStore _customCssStore;
    private readonly ILogger<EffectiveSettingsProvider> _logger;
    private AdminManagedSettingsDocument _managedSettingsDocument;
    private EffectiveSettingsSnapshot _snapshot;

    public EffectiveSettingsProvider(
        BootstrapServerSettingsHolder bootstrapSettingsHolder,
        IAdminManagedSettingsStore store,
        ICustomCssStore customCssStore,
        ILogger<EffectiveSettingsProvider> logger)
    {
        _bootstrapSettings = ServerSettingsFactory.Clone(bootstrapSettingsHolder.Settings);
        _store = store;
        _customCssStore = customCssStore;
        _logger = logger;

        var storedSettings = _store.LoadOrSeed(_bootstrapSettings);
        _managedSettingsDocument = CloneManagedSettingsDocument(storedSettings);
        _snapshot = BuildSnapshot(
            1,
            _managedSettingsDocument,
            _customCssStore.LoadEditableCss(),
            _customCssStore.LoadStoredCssOverride() is not null);
        _store.Save(CloneManagedSettingsDocument(_managedSettingsDocument));
    }

    public EffectiveSettingsSnapshot GetCurrentSnapshot()
    {
        lock (_sync)
        {
            return CloneSnapshot(_snapshot);
        }
    }

    SettingsSnapshot ISettingsSnapshotProvider.GetCurrentSnapshot()
    {
        lock (_sync)
        {
            return new SettingsSnapshot(_snapshot.Version, ServerSettingsFactory.Clone(_snapshot.Settings));
        }
    }

    public EffectiveSettingsSnapshot Update(AdminManagedSettingsDocument settings, string? customCss)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        lock (_sync)
        {
            var currentSnapshot = _snapshot;
            var currentDocument = CloneManagedSettingsDocument(_managedSettingsDocument);
            var nextManagedDocument = MergeManagedSettingsDocument(currentDocument, settings);
            var fallbackCustomCss = _customCssStore.LoadFallbackCss();
            var hasCustomCssOverride = customCss is not null && !string.Equals(
                NormalizeCustomCss(customCss),
                NormalizeCustomCss(fallbackCustomCss),
                StringComparison.Ordinal);
            var nextSnapshot = BuildSnapshot(
                currentSnapshot.Version + 1,
                nextManagedDocument,
                hasCustomCssOverride ? customCss! : fallbackCustomCss,
                hasCustomCssOverride);

            _store.Save(CloneManagedSettingsDocument(nextManagedDocument));

            try
            {
                _customCssStore.Save(nextSnapshot.HasCustomCssOverride ? nextSnapshot.CustomCss : null);
                string loadedCustomCss;
                try
                {
                    loadedCustomCss = _customCssStore.LoadEditableCss();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reload saved custom CSS; using provided value.");
                    loadedCustomCss = nextSnapshot.CustomCss;
                }

                _managedSettingsDocument = nextManagedDocument;
                _snapshot = nextSnapshot with { CustomCss = loadedCustomCss };
                return CloneSnapshot(_snapshot);
            }
            catch (Exception ex)
            {
                try
                {
                    _store.Save(currentDocument);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogCritical(rollbackEx, "Failed to roll back admin-managed settings after custom CSS persistence failed.");
                }

                throw new InvalidOperationException("Failed to persist custom CSS.", ex);
            }
        }
    }

    public IServerSettings GetCurrentSettings()
    {
        return ((ISettingsSnapshotProvider)this).GetCurrentSnapshot().Settings;
    }

    public long GetCurrentVersion()
    {
        return ((ISettingsSnapshotProvider)this).GetCurrentSnapshot().Version;
    }

    private EffectiveSettingsSnapshot BuildSnapshot(long version, AdminManagedSettingsDocument managedSettings, string customCss, bool hasCustomCssOverride)
    {
        var effectiveSettings = ServerSettingsFactory.Clone(_bootstrapSettings);

        managedSettings.Normalize();
        managedSettings.General.ApplyTo(effectiveSettings.GeneralSettingsImpl ??= new GeneralSettings());

        var effectiveAccounts = effectiveSettings.AccountsImpl.ToList();
        var effectiveAccountsById = new Dictionary<string, ServerAccountSettings>(StringComparer.Ordinal);
        foreach (var effectiveAccount in effectiveAccounts)
        {
            if (string.IsNullOrWhiteSpace(effectiveAccount.AccountIdentifier))
            {
                _logger.LogWarning("Skipping effective account for runtime settings because it is missing an AccountIdentifier.");
                continue;
            }

            var identifier = effectiveAccount.AccountIdentifier;
            if (!effectiveAccountsById.TryAdd(identifier, effectiveAccount))
            {
                _logger.LogWarning("Duplicate effective account identifier {accountIdentifier} detected while building runtime settings.", identifier);
            }
        }

        foreach (var managedAccount in managedSettings.Accounts)
        {
            if (string.IsNullOrWhiteSpace(managedAccount.AccountIdentifier))
            {
                _logger.LogWarning("Skipping admin-managed account override because it is missing an AccountIdentifier.");
                continue;
            }

            if (!effectiveAccountsById.TryGetValue(managedAccount.AccountIdentifier, out var effectiveAccount))
            {
                _logger.LogWarning("Skipping admin-managed account override for unknown account identifier {accountIdentifier}.", managedAccount.AccountIdentifier);
                continue;
            }

            managedAccount.ApplyTo(effectiveAccount);
        }

        effectiveSettings.AccountsImpl = effectiveAccounts;
        effectiveSettings.Validate();

        return new EffectiveSettingsSnapshot(version, effectiveSettings, customCss, hasCustomCssOverride);
    }

    private static EffectiveSettingsSnapshot CloneSnapshot(EffectiveSettingsSnapshot snapshot)
    {
        return new EffectiveSettingsSnapshot(
            snapshot.Version,
            ServerSettingsFactory.Clone(snapshot.Settings),
            snapshot.CustomCss,
            snapshot.HasCustomCssOverride);
    }

    private void ReplaceOrAppendManagedAccount(List<AdminManagedAccountSettings> accounts, AdminManagedAccountSettings account)
    {
        if (string.IsNullOrWhiteSpace(account.AccountIdentifier))
        {
            _logger.LogWarning("Skipping admin-managed account update because it is missing an AccountIdentifier.");
            return;
        }

        var existingIndex = accounts.FindIndex(existing =>
            string.Equals(existing.AccountIdentifier, account.AccountIdentifier, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            accounts[existingIndex] = account;
            return;
        }

        accounts.Add(account);
    }

    private AdminManagedSettingsDocument MergeManagedSettingsDocument(
        AdminManagedSettingsDocument currentDocument,
        AdminManagedSettingsDocument incomingDocument)
    {
        currentDocument.Normalize();
        incomingDocument.Normalize();

        var mergedDocument = CloneManagedSettingsDocument(currentDocument);
        mergedDocument.General = CloneManagedGeneralSettings(incomingDocument.General);

        foreach (var incomingAccount in incomingDocument.Accounts.Select(CloneManagedAccountSettings))
        {
            ReplaceOrAppendManagedAccount(mergedDocument.Accounts, incomingAccount);
        }

        mergedDocument.Normalize();
        return mergedDocument;
    }

    private static AdminManagedSettingsDocument CloneManagedSettingsDocument(AdminManagedSettingsDocument document)
    {
        return JsonSerializer.Deserialize<AdminManagedSettingsDocument>(JsonSerializer.Serialize(document))
            ?? new AdminManagedSettingsDocument();
    }

    private static AdminManagedGeneralSettings CloneManagedGeneralSettings(AdminManagedGeneralSettings settings)
    {
        return JsonSerializer.Deserialize<AdminManagedGeneralSettings>(JsonSerializer.Serialize(settings))
            ?? new AdminManagedGeneralSettings();
    }

    private static AdminManagedAccountSettings CloneManagedAccountSettings(AdminManagedAccountSettings settings)
    {
        return JsonSerializer.Deserialize<AdminManagedAccountSettings>(JsonSerializer.Serialize(settings))
            ?? new AdminManagedAccountSettings();
    }

    private static string NormalizeCustomCss(string? css)
    {
        return (css ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
