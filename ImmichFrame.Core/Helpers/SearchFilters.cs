using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Helpers;

/// <summary>
/// Builds the Immich (v3.2+) search filter for an account, so Immich does the source selection
/// and filtering server-side instead of ImmichFrame downloading every album/person/tag and
/// filtering the lists itself. Ported from upstream immichFrame/ImmichFrame (#712), keeping this
/// fork's existing filter semantics: inclusive date bounds and an exact rating match.
/// </summary>
public static class SearchFilters
{
    /// <summary>Only assets that are not in the trash (serialized as <c>{"eq": null}</c>).</summary>
    public static NullableDateFilter NotTrashed() => new() { Eq = null };

    public static EnumFilterAssetVisibility Visibility(IAccountSettings settings)
    {
        var visibilities = new List<AssetVisibility> { AssetVisibility.Timeline };
        if (settings.ShowArchived)
        {
            visibilities.Add(AssetVisibility.Archive);
        }

        return new EnumFilterAssetVisibility { In = visibilities };
    }

    public static IdsFilter? ExcludedAlbums(IAccountSettings settings)
    {
        if (settings.ExcludedAlbums is not { Count: > 0 } excluded)
        {
            return null;
        }

        return new IdsFilter { None = excluded };
    }

    public static EnumFilterAssetType Types(IAccountSettings settings)
    {
        var types = new List<AssetTypeEnum> { AssetTypeEnum.IMAGE };
        if (settings.ShowVideos)
        {
            types.Add(AssetTypeEnum.VIDEO);
        }

        return new EnumFilterAssetType { In = types };
    }

    /// <summary>True when the account limits itself to favorites, albums, people or tags.</summary>
    public static bool HasConfiguredSource(IAccountSettings settings) =>
        settings.ShowFavorites
        || settings.Albums is { Count: > 0 }
        || settings.People is { Count: > 0 }
        || settings.Tags is { Count: > 0 };

    /// <summary>
    /// The full filter: global constraints (not trashed, visibility, type, excluded albums, date
    /// range, rating) ANDed with an OR of the configured sources. No sources means the whole library.
    /// </summary>
    public static SearchFilter ForAccount(IAccountSettings settings, IReadOnlyCollection<Guid> tagIds)
    {
        var filter = new SearchFilter
        {
            TrashedAt = NotTrashed(),
            Visibility = Visibility(settings),
            Type = Types(settings),
            AlbumIds = ExcludedAlbums(settings),
            TakenAt = TakenAt(settings),
            Rating = Rating(settings)
        };

        var branches = Branches(settings, tagIds);
        if (branches.Count > 0)
        {
            filter.Or = branches;
        }

        return filter;
    }

    /// <summary>
    /// False when sources are configured but none of them can match (e.g. only tags that don't
    /// exist on the server): searching then would wrongly fall back to the whole library.
    /// </summary>
    public static bool HasMatchingBranch(IAccountSettings settings, IReadOnlyCollection<Guid> tagIds) =>
        Branches(settings, tagIds).Count > 0;

    private static List<SearchFilterBranch> Branches(IAccountSettings settings, IReadOnlyCollection<Guid> tagIds)
    {
        var branches = new List<SearchFilterBranch>();
        if (settings.ShowFavorites)
        {
            branches.Add(new SearchFilterBranch { IsFavorite = new BoolFilter { Eq = true } });
        }

        if (settings.Albums is { Count: > 0 } albums)
        {
            branches.Add(new SearchFilterBranch { AlbumIds = new IdsFilter { Any = albums } });
        }

        if (settings.People is { Count: > 0 } people)
        {
            branches.Add(new SearchFilterBranch { PersonIds = new IdsFilter { Any = people } });
        }

        if (tagIds.Count > 0)
        {
            branches.Add(new SearchFilterBranch { TagIds = new IdsFilter { Any = tagIds.ToList() } });
        }

        return branches;
    }

    // Inclusive on both ends, matching this fork's previous client-side filter
    // (DateTimeOriginal >= from && DateTimeOriginal <= until).
    private static DateFilter? TakenAt(IAccountSettings settings)
    {
        var takenAt = new DateFilter();
        var hasTakenFilter = false;
        if (settings.ImagesUntilDate is DateTime until)
        {
            takenAt.Lte = until;
            hasTakenFilter = true;
        }

        DateTime? from = settings.ImagesFromDate
            ?? (settings.ImagesFromDays is int days ? DateTime.Today.AddDays(-days) : null);
        if (from is DateTime after)
        {
            takenAt.Gte = after;
            hasTakenFilter = true;
        }

        return hasTakenFilter ? takenAt : null;
    }

    // Exact match, as before (upstream switched to "at least N stars").
    private static NumberFilterNullable? Rating(IAccountSettings settings) =>
        settings.Rating is int rating ? new NumberFilterNullable { Eq = rating } : null;
}
