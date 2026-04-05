namespace ImmichFrame.Core.Interfaces;

public sealed record SettingsSnapshot(long Version, IServerSettings Settings);

public interface ISettingsSnapshotProvider
{
    SettingsSnapshot GetCurrentSnapshot();
    IServerSettings GetCurrentSettings();
    long GetCurrentVersion();
}
