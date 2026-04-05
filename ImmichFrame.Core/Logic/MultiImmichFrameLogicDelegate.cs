using System.Collections.Frozen;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic;

public class MultiImmichFrameLogicDelegate : IImmichFrameLogic
{
    private readonly object _sync = new();
    private readonly ISettingsSnapshotProvider _settingsProvider;
    private readonly Func<IAccountSelectionStrategy> _accountSelectionStrategyFactory;
    private DelegateState? _state;
    private readonly ILogger<MultiImmichFrameLogicDelegate> _logger;

    public MultiImmichFrameLogicDelegate(
        ISettingsSnapshotProvider settingsProvider,
        Func<IAccountSettings, IAccountImmichFrameLogic> logicFactory,
        Func<IAccountSelectionStrategy> accountSelectionStrategyFactory,
        ILogger<MultiImmichFrameLogicDelegate> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsProvider = settingsProvider;
        _accountSelectionStrategyFactory = accountSelectionStrategyFactory;
        LogicFactory = logicFactory;
    }

    private Func<IAccountSettings, IAccountImmichFrameLogic> LogicFactory { get; }

    public async Task<AssetResponseDto?> GetNextAsset() => (await GetState().AccountSelectionStrategy.GetNextAsset())?.ToAsset();

    public async Task<IEnumerable<AssetResponseDto>> GetAssets()
        => (await GetState().AccountSelectionStrategy.GetAssets()).Shuffle().Select(it => it.ToAsset());


    public Task<AssetResponseDto> GetAssetInfoById(Guid assetId)
        => GetState().AccountSelectionStrategy.ForAsset(assetId, async logic => (await logic.GetAssetInfoById(assetId)).WithAccount(logic));


    public Task<IEnumerable<AlbumResponseDto>> GetAlbumInfoById(Guid assetId)
        => GetState().AccountSelectionStrategy.ForAsset(assetId, logic => logic.GetAlbumInfoById(assetId));


    public Task<AssetResponse> GetAsset(Guid assetId, AssetTypeEnum? assetType = null, string? rangeHeader = null)
        => GetState().AccountSelectionStrategy.ForAsset(assetId, logic => logic.GetAsset(assetId, assetType, rangeHeader));

    public async Task<long> GetTotalAssets()
    {
        var allInts = await Task.WhenAll(GetState().AccountToDelegate.Values.Select(account => account.GetTotalAssets()));
        return allInts.Sum();
    }

    public Task SendWebhookNotification(IWebhookNotification notification) =>
        WebhookHelper.SendWebhookNotification(notification, GetState().Settings.GeneralSettings.Webhook);

    private DelegateState GetState()
    {
        var snapshot = _settingsProvider.GetCurrentSnapshot();

        lock (_sync)
        {
            if (_state != null && _state.Version == snapshot.Version)
            {
                return _state;
            }

            _logger.LogInformation("Reloading effective settings snapshot version {version}.", snapshot.Version);
            _state = BuildState(snapshot);
            return _state;
        }
    }

    private DelegateState BuildState(SettingsSnapshot snapshot)
    {
        var settings = snapshot.Settings;
        var accountToDelegate = settings.Accounts
            .ToFrozenDictionary(keySelector: account => account, elementSelector: LogicFactory);

        var accountSelectionStrategy = _accountSelectionStrategyFactory();
        accountSelectionStrategy.Initialize(accountToDelegate.Values.ToList());

        return new DelegateState(snapshot.Version, settings, accountToDelegate, accountSelectionStrategy);
    }

    private sealed record DelegateState(
        long Version,
        IServerSettings Settings,
        FrozenDictionary<IAccountSettings, IAccountImmichFrameLogic> AccountToDelegate,
        IAccountSelectionStrategy AccountSelectionStrategy);
}

public static class AccountAndAssetExtensions
{
    public static AssetResponseDto ToAsset(this (IAccountImmichFrameLogic, AssetResponseDto) accountAndAsset)
    {
        var (account, asset) = accountAndAsset;
        return asset.WithAccount(account);
    }

    public static AssetResponseDto WithAccount(this AssetResponseDto asset, IAccountImmichFrameLogic account)
    {
        asset.ImmichServerUrl = account.AccountSettings.ImmichServerUrl;
        return asset;
    }
}
