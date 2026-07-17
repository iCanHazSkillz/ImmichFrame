// ImmichFrame.Core/Helpers/AssetHelper.cs
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ImmichFrame.Core.Helpers;

public static class AssetHelper
{
    // Shared across AlbumAssetsPool, PersonAssetsPool, and TagAssetsPool so an account with many
    // configured albums/people/tags doesn't fire off unbounded concurrent paginated searches
    // against the same Immich server at once.
    public const int DefaultPaginationConcurrencyLimit = 4;

    public static async Task<TResult[]> RunWithConcurrencyLimitAsync<TInput, TResult>(
        IEnumerable<TInput> items,
        Func<TInput, Task<TResult>> action,
        int concurrencyLimit = DefaultPaginationConcurrencyLimit,
        CancellationToken ct = default)
    {
        using var semaphore = new SemaphoreSlim(concurrencyLimit);

        async Task<TResult> RunAsync(TInput item)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        }

        return await Task.WhenAll(items.Select(RunAsync));
    }

    public static async Task<IEnumerable<AssetResponseDto>> GetExcludedAlbumAssets(ImmichApi immichApi, IAccountSettings accountSettings, ILogger? logger = null, CancellationToken ct = default)
    {
        var excludedAlbums = accountSettings?.ExcludedAlbums ?? new();

        // Each excluded album is paginated independently; fetch them concurrently (up to a
        // shared limit) instead of one at a time so accounts with many excluded albums don't pay
        // for N sequential paginated fetches in a row.
        var perAlbumAssets = await RunWithConcurrencyLimitAsync(
            excludedAlbums,
            albumId => LoadExcludedAlbumAssets(immichApi, albumId, accountSettings?.ImmichServerUrl, logger, ct),
            ct: ct);

        return perAlbumAssets.SelectMany(assets => assets);
    }

    private static async Task<List<AssetResponseDto>> LoadExcludedAlbumAssets(ImmichApi immichApi, Guid albumId, string? immichServerUrl, ILogger? logger, CancellationToken ct)
    {
        var excludedAlbumAssets = new List<AssetResponseDto>();

        int page = 1;
        int batchSize = 1000;
        int itemsInPage;
        do
        {
            var metadataBody = new MetadataSearchDto
            {
                Page = page,
                Size = batchSize,
                AlbumIds = [albumId]
            };

            SearchResponseDto searchResponse;
            try
            {
                searchResponse = await immichApi.SearchAssetsAsync(null, null, metadataBody, ct);
            }
            catch (ApiException ex) when (IsExpectedAlbumLookupFailure(ex))
            {
                LogSkippedAlbum(logger, albumId, immichServerUrl, "excluded", ex);
                break;
            }

            itemsInPage = searchResponse.Assets?.Items?.Count ?? 0;

            if (searchResponse.Assets?.Items != null)
            {
                excludedAlbumAssets.AddRange(searchResponse.Assets.Items);
            }

            page++;
        } while (itemsInPage == batchSize);

        return excludedAlbumAssets;
    }

    public static bool IsExpectedAlbumLookupFailure(ApiException ex)
    {
        return ex.StatusCode is 400 or 404;
    }

    public static void LogSkippedAlbum(ILogger? logger, Guid albumId, string? immichServerUrl, string albumListType, ApiException ex)
    {
        if (logger == null)
        {
            return;
        }

        logger.LogWarning(
            ex,
            "Skipping {albumListType} album {albumId} for Immich server {immichServerUrl}: Immich returned {statusCode}. Response: {immichResponse}. CorrelationId: {correlationId}",
            albumListType,
            albumId,
            immichServerUrl,
            ex.StatusCode,
            ex.Response,
            TryGetCorrelationId(ex.Response));
    }

    public static string? TryGetCorrelationId(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("correlationId", out var correlationId) ||
                correlationId.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return correlationId.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
