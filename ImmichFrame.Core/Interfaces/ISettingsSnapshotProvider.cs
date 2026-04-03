namespace ImmichFrame.Core.Interfaces;

public interface ISettingsSnapshotProvider
{
    IServerSettings GetCurrentSettings();
    long GetCurrentVersion();
}
