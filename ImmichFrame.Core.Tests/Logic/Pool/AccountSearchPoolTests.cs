using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class AccountSearchPoolTests
{
    private Mock<IApiCache> _cache;
    private Mock<ImmichApi> _api;
    private Mock<IAccountSettings> _settings;
    private AccountSearchPool _pool;

    [SetUp]
    public void Setup()
    {
        _cache = new Mock<IApiCache>();
        _cache.Setup(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<Func<Task<ICollection<TagResponseDto>>>>()))
            .Returns<string, Func<Task<ICollection<TagResponseDto>>>>((_, factory) => factory());
        _cache.Setup(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<Func<Task<long>>>()))
            .Returns<string, Func<Task<long>>>((_, factory) => factory());
        _api = new Mock<ImmichApi>(null, null);
        _api.Setup(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetResponseDto>());
        _api.Setup(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStatisticsResponseDto { Total = 0 });
        _settings = new Mock<IAccountSettings>();
        _settings.SetupGet(s => s.ImmichServerUrl).Returns("https://photos.example");
        _settings.SetupGet(s => s.Albums).Returns(new List<Guid>());
        _settings.SetupGet(s => s.People).Returns(new List<Guid>());
        _settings.SetupGet(s => s.Tags).Returns(new List<string>());
        _settings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid>());
        _pool = new AccountSearchPool(_cache.Object, _api.Object, _settings.Object);
    }

    [Test]
    public async Task GetAssets_WithoutSources_SearchesTheLibrary()
    {
        await _pool.GetAssets(5);

        VerifyRandom(dto =>
            dto.Size == 5 &&
            dto.WithExif == true &&
            dto.WithPeople == true &&
            dto.Filter!.Or == null &&
            dto.Filter.TrashedAt != null &&
            dto.Filter.AlbumIds == null &&
            dto.Filter.Visibility!.In.Contains(AssetVisibility.Timeline) &&
            !dto.Filter.Visibility.In.Contains(AssetVisibility.Archive) &&
            dto.Filter.Type!.In.Contains(AssetTypeEnum.IMAGE) &&
            !dto.Filter.Type.In.Contains(AssetTypeEnum.VIDEO));
    }

    [Test]
    public async Task GetAssets_ShowVideos_AddsVideoType()
    {
        _settings.SetupGet(s => s.ShowVideos).Returns(true);

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.Type!.In.Contains(AssetTypeEnum.IMAGE) &&
            dto.Filter.Type.In.Contains(AssetTypeEnum.VIDEO));
    }

    [Test]
    public async Task GetAssets_ShowArchived_AddsArchiveVisibility()
    {
        _settings.SetupGet(s => s.ShowArchived).Returns(true);

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.Visibility!.In.Contains(AssetVisibility.Timeline) &&
            dto.Filter.Visibility.In.Contains(AssetVisibility.Archive));
    }

    [Test]
    public async Task GetAssets_Rating_MatchesExactly()
    {
        // Fork keeps exact-match rating semantics (upstream uses "at least").
        _settings.SetupGet(s => s.Rating).Returns(3);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.Rating!.Eq == 3 && dto.Filter.Rating.Gte == null);
    }

    [Test]
    public async Task GetAssets_ImagesFromDays_SetsInclusiveTakenAfter()
    {
        _settings.SetupGet(s => s.ImagesFromDays).Returns(10);
        var expected = DateTime.Today.AddDays(-10);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TakenAt!.Gte.HasValue && dto.Filter.TakenAt.Gte.Value.Date == expected.Date && dto.Filter.TakenAt.Gt == null);
    }

    [Test]
    public async Task GetAssets_ImagesFromDate_WinsOverFromDays()
    {
        var fromDate = new DateTime(2020, 6, 1);
        _settings.SetupGet(s => s.ImagesFromDate).Returns(fromDate);
        _settings.SetupGet(s => s.ImagesFromDays).Returns(10);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TakenAt!.Gte.HasValue && dto.Filter.TakenAt.Gte.Value.Date == fromDate.Date);
    }

    [Test]
    public async Task GetAssets_ImagesUntilDate_SetsInclusiveTakenBefore()
    {
        var until = new DateTime(2024, 1, 1);
        _settings.SetupGet(s => s.ImagesUntilDate).Returns(until);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TakenAt!.Lte.HasValue && dto.Filter.TakenAt.Lte.Value.Date == until.Date && dto.Filter.TakenAt.Lt == null);
    }

    [Test]
    public async Task GetAssets_ExcludedAlbums_SentAsNone()
    {
        var excludedAlbumId = Guid.NewGuid();
        _settings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { excludedAlbumId });

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.AlbumIds!.None.Contains(excludedAlbumId));
        _api.Verify(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MetadataSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssets_EmptyExcludedAlbums_OmitsAlbumFilter()
    {
        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.AlbumIds == null);
    }

    [Test]
    public async Task GetAssets_NullExcludedAlbums_OmitsAlbumFilter()
    {
        _settings.SetupGet(s => s.ExcludedAlbums).Returns((List<Guid>)null!);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.AlbumIds == null);
    }

    [Test]
    public async Task GetAssets_WithSources_OrsOnlyConfiguredBranches()
    {
        var albumId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var excludedAlbumId = Guid.NewGuid();
        _settings.SetupGet(s => s.ShowFavorites).Returns(true);
        _settings.SetupGet(s => s.Albums).Returns(new List<Guid> { albumId });
        _settings.SetupGet(s => s.People).Returns(new List<Guid> { personId });
        _settings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { excludedAlbumId });

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.AlbumIds!.None.Contains(excludedAlbumId) &&
            dto.Filter.Or.Count == 3 &&
            dto.Filter.Or.Any(branch => branch.IsFavorite != null && branch.IsFavorite.Eq) &&
            dto.Filter.Or.Any(branch => branch.AlbumIds != null && branch.AlbumIds.Any.Contains(albumId) && branch.AlbumIds.None == null) &&
            dto.Filter.Or.Any(branch => branch.PersonIds != null && branch.PersonIds.Any.Contains(personId)));
    }

    [Test]
    public async Task GetAssets_Tags_MatchFullPathCaseSensitively()
    {
        var match = Guid.NewGuid();
        var wrongCase = Guid.NewGuid();
        var wrongParent = Guid.NewGuid();
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "Parent/Child" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto>
            {
                new() { Id = match, Value = "Parent/Child" },
                new() { Id = wrongCase, Value = "parent/child" },
                new() { Id = wrongParent, Value = "Other/Child" }
            });

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.Or.Count == 1 &&
            dto.Filter.Or.Single().TagIds!.Any.Contains(match) &&
            !dto.Filter.Or.Single().TagIds.Any.Contains(wrongCase) &&
            !dto.Filter.Or.Single().TagIds.Any.Contains(wrongParent));
    }

    [Test]
    public async Task GetAssets_UnmatchedTagsOnly_DoesNotSearch()
    {
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "missing" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto>());

        var result = await _pool.GetAssets(5);

        Assert.That(result, Is.Empty);
        _api.Verify(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssetCount_UsesTheSameFilter()
    {
        _settings.SetupGet(s => s.ShowFavorites).Returns(true);
        _api.Setup(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStatisticsResponseDto { Total = 12 });

        var count = await _pool.GetAssetCount();

        Assert.That(count, Is.EqualTo(12));
        _api.Verify(a => a.SearchAssetStatisticsAsync(It.Is<StatisticsSearchDto>(dto =>
            dto.Filter!.Or.Any(branch => branch.IsFavorite != null && branch.IsFavorite.Eq) &&
            dto.Filter.Visibility!.In.Contains(AssetVisibility.Timeline)
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAssetCount_UnmatchedTagsOnly_IsZeroWithoutStatisticsCall()
    {
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "missing" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto>());

        var count = await _pool.GetAssetCount();

        Assert.That(count, Is.EqualTo(0));
        _api.Verify(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssetCount_SameFilter_IsServedFromCache()
    {
        using var cache = new ApiCache(TimeSpan.FromHours(1));
        var pool = new AccountSearchPool(cache, _api.Object, _settings.Object);
        _api.Setup(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStatisticsResponseDto { Total = 7 });

        Assert.That(await pool.GetAssetCount(), Is.EqualTo(7));
        Assert.That(await pool.GetAssetCount(), Is.EqualTo(7));

        _api.Verify(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAssets_WithoutTags_DoesNotFetchAssetInfo()
    {
        _settings.SetupGet(s => s.Albums).Returns(new List<Guid> { Guid.NewGuid() });
        _api.Setup(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetResponseDto> { new() { Id = Guid.NewGuid() } });

        await _pool.GetAssets(1);

        _api.Verify(a => a.GetAssetInfoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _api.Verify(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssets_WithTags_AttachesOnlyConfiguredTagsForCaptions()
    {
        // Random search doesn't return tags; the frame's tag caption reads asset.Tags, so the pool
        // fills in the configured tags each asset carries (as the old tag pool did).
        var configured = new TagResponseDto { Id = Guid.NewGuid(), Value = "Family" };
        var other = new TagResponseDto { Id = Guid.NewGuid(), Value = "Android" };
        var assetId = Guid.NewGuid();
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "Family" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto> { configured, other });
        _api.Setup(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetResponseDto> { new() { Id = assetId } });
        _api.Setup(a => a.GetAssetInfoAsync(assetId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetResponseDto { Id = assetId, Tags = new List<TagResponseDto> { configured, other } });

        var asset = (await _pool.GetAssets(1)).Single();

        Assert.That(asset.Tags.Select(t => t.Id), Is.EqualTo(new[] { configured.Id }));
    }

    [Test]
    public void ForAccount_SerializesNotTrashedAsExplicitNull()
    {
        // Immich needs {"eq": null} to mean "not trashed"; the generated DTO would drop a null Eq.
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(SearchFilters.ForAccount(_settings.Object, []));

        Assert.That(json, Does.Contain("\"trashedAt\":{\"eq\":null}"));
    }

    [Test]
    public void ForAccount_SerializesInclusiveDateBounds()
    {
        _settings.SetupGet(s => s.ImagesFromDate).Returns(new DateTime(2020, 6, 1));
        _settings.SetupGet(s => s.ImagesUntilDate).Returns(new DateTime(2024, 1, 1));

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(SearchFilters.ForAccount(_settings.Object, []));

        Assert.That(json, Does.Contain("\"gte\":\"2020-06-01").And.Contain("\"lte\":\"2024-01-01"));
    }

    private void VerifyRandom(Func<RandomSearchDto, bool> match) =>
        _api.Verify(a => a.SearchRandomAsync(It.Is<RandomSearchDto>(dto => match(dto)), It.IsAny<CancellationToken>()), Times.Once);
}
