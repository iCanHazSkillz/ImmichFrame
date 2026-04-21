// ImmichFrame.Core/Helpers/AssetHelper.cs
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ImmichFrame.Core.Helpers;

public static class AssetHelper
{
    public static async Task<IEnumerable<AssetResponseDto>> GetExcludedAlbumAssets(ImmichApi immichApi, IAccountSettings accountSettings, ILogger? logger = null, CancellationToken ct = default)
    {
        var excludedAlbumAssets = new List<AssetResponseDto>();

        foreach (var albumId in accountSettings?.ExcludedAlbums ?? new())
        {
            AlbumResponseDto albumInfo;
            try
            {
                albumInfo = await immichApi.GetAlbumInfoAsync(albumId, null, null, ct);
            }
            catch (ApiException ex) when (IsExpectedAlbumLookupFailure(ex))
            {
                LogSkippedAlbum(logger, albumId, accountSettings?.ImmichServerUrl, "excluded", ex);
                continue;
            }

            if (albumInfo.Assets != null)
            {
                excludedAlbumAssets.AddRange(albumInfo.Assets);
            }
        }

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
            return document.RootElement.TryGetProperty("correlationId", out var correlationId)
                ? correlationId.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
