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

public sealed record EffectiveSettingsSnapshot(long Version, ServerSettings Settings);

public interface IWritableEffectiveSettingsProvider : ISettingsSnapshotProvider
{
    EffectiveSettingsSnapshot GetCurrentSnapshot();
    EffectiveSettingsSnapshot Update(AdminManagedSettingsDocument settings);
}

public sealed class EffectiveSettingsProvider : IWritableEffectiveSettingsProvider
{
    private readonly object _sync = new();
    private readonly ServerSettings _bootstrapSettings;
    private readonly IAdminManagedSettingsStore _store;
    private EffectiveSettingsSnapshot _snapshot;

    public EffectiveSettingsProvider(
        BootstrapServerSettingsHolder bootstrapSettingsHolder,
        IAdminManagedSettingsStore store)
    {
        _bootstrapSettings = ServerSettingsFactory.Clone(bootstrapSettingsHolder.Settings);
        _store = store;

        var storedSettings = _store.LoadOrSeed(_bootstrapSettings);
        _snapshot = BuildSnapshot(1, storedSettings);
        _store.Save(AdminManagedSettingsDocument.FromServerSettings(_snapshot.Settings));
    }

    public EffectiveSettingsSnapshot GetCurrentSnapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    public EffectiveSettingsSnapshot Update(AdminManagedSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        lock (_sync)
        {
            var nextSnapshot = BuildSnapshot(_snapshot.Version + 1, settings);
            _store.Save(AdminManagedSettingsDocument.FromServerSettings(nextSnapshot.Settings));
            _snapshot = nextSnapshot;
            return _snapshot;
        }
    }

    public IServerSettings GetCurrentSettings()
    {
        return GetCurrentSnapshot().Settings;
    }

    public long GetCurrentVersion()
    {
        return GetCurrentSnapshot().Version;
    }

    private EffectiveSettingsSnapshot BuildSnapshot(long version, AdminManagedSettingsDocument managedSettings)
    {
        var effectiveSettings = ServerSettingsFactory.Clone(_bootstrapSettings);

        managedSettings.Normalize();
        managedSettings.General.ApplyTo(effectiveSettings.GeneralSettingsImpl ??= new GeneralSettings());

        var effectiveAccounts = effectiveSettings.AccountsImpl.ToList();
        for (var index = 0; index < effectiveAccounts.Count; index++)
        {
            if (index >= managedSettings.Accounts.Count)
            {
                continue;
            }

            managedSettings.Accounts[index].ApplyTo(effectiveAccounts[index]);
        }

        effectiveSettings.AccountsImpl = effectiveAccounts;
        effectiveSettings.Validate();

        return new EffectiveSettingsSnapshot(version, effectiveSettings);
    }
}
