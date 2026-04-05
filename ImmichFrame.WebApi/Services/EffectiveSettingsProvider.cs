using ImmichFrame.WebApi.Models;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.WebApi.Services;

public sealed class BootstrapServerSettingsHolder
{
    public BootstrapServerSettingsHolder(ServerSettings settings)
    {
        Settings = ServerSettingsFactory.Clone(settings);
    }

    public ServerSettings Settings { get; }
}

public sealed record EffectiveSettingsSnapshot(long Version, ServerSettings Settings, string CustomCss);

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
        _snapshot = BuildSnapshot(1, storedSettings, _customCssStore.LoadEditableCss());
        _store.Save(AdminManagedSettingsDocument.FromServerSettings(_snapshot.Settings));
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
            var currentDocument = AdminManagedSettingsDocument.FromServerSettings(currentSnapshot.Settings);
            var nextSnapshot = BuildSnapshot(currentSnapshot.Version + 1, settings, customCss ?? string.Empty);

            _store.Save(AdminManagedSettingsDocument.FromServerSettings(nextSnapshot.Settings));

            try
            {
                _customCssStore.Save(nextSnapshot.CustomCss);
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

            _snapshot = nextSnapshot with { CustomCss = _customCssStore.LoadEditableCss() };
            return CloneSnapshot(_snapshot);
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

    private EffectiveSettingsSnapshot BuildSnapshot(long version, AdminManagedSettingsDocument managedSettings, string customCss)
    {
        var effectiveSettings = ServerSettingsFactory.Clone(_bootstrapSettings);

        managedSettings.Normalize();
        managedSettings.General.ApplyTo(effectiveSettings.GeneralSettingsImpl ??= new GeneralSettings());

        var effectiveAccounts = effectiveSettings.AccountsImpl.ToList();
        var effectiveAccountsById = new Dictionary<string, ServerAccountSettings>(StringComparer.Ordinal);
        foreach (var effectiveAccount in effectiveAccounts)
        {
            var identifier = ServerSettingsFactory.BuildAccountIdentifier(effectiveAccount);
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

        return new EffectiveSettingsSnapshot(version, effectiveSettings, customCss);
    }

    private static EffectiveSettingsSnapshot CloneSnapshot(EffectiveSettingsSnapshot snapshot)
    {
        return new EffectiveSettingsSnapshot(
            snapshot.Version,
            ServerSettingsFactory.Clone(snapshot.Settings),
            snapshot.CustomCss);
    }
}
