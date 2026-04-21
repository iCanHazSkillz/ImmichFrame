using NUnit.Framework;
using Moq;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class AlbumAssetsPoolTests
{
    private Mock<IApiCache> _mockApiCache;
    private Mock<ImmichApi> _mockImmichApi;
    private Mock<IAccountSettings> _mockAccountSettings;
    private Mock<ILogger<AlbumAssetsPool>> _mockLogger;
    private AlbumAssetsPool _albumAssetsPool;

    [SetUp]
    public void Setup()
    {
        _mockApiCache = new Mock<IApiCache>();

        _mockApiCache
            .Setup(m => m.GetOrAddAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<IEnumerable<AssetResponseDto>>>>()))
            .Returns<string, Func<Task<IEnumerable<AssetResponseDto>>>>((_, factory) => factory());

        _mockImmichApi = new Mock<ImmichApi>("", null);
        _mockAccountSettings = new Mock<IAccountSettings>();
        _mockLogger = new Mock<ILogger<AlbumAssetsPool>>();
        _albumAssetsPool = new AlbumAssetsPool(_mockApiCache.Object, _mockImmichApi.Object, _mockAccountSettings.Object, _mockLogger.Object);

        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid>());
        _mockAccountSettings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid>());
        _mockAccountSettings.SetupGet(s => s.ImmichServerUrl).Returns("http://immich.example");
    }

    private AssetResponseDto CreateAsset(string id) => new AssetResponseDto { Id = id, Type = AssetTypeEnum.IMAGE };

    [Test]
    public async Task LoadAssets_ReturnsAssetsPresentIIncludedNotExcludedAlbums()
    {
        // Arrange
        var album1Id = Guid.NewGuid();
        var excludedAlbumId = Guid.NewGuid();

        var assetA = CreateAsset("A"); // In album1
        var assetB = CreateAsset("B"); // In album1 and excludedAlbum
        var assetC = CreateAsset("C"); // In excludedAlbum only
        var assetD = CreateAsset("D"); // In album1 only

        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid> { album1Id });
        _mockAccountSettings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { excludedAlbumId });

        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(album1Id, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumResponseDto { Assets = new List<AssetResponseDto> { assetA, assetB, assetD } });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(excludedAlbumId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumResponseDto { Assets = new List<AssetResponseDto> { assetB, assetC } });

        // Act
        var result = (await _albumAssetsPool.GetAssets(25)).ToList();

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.Any(a => a.Id == "A"));
        Assert.That(result.Any(a => a.Id == "D"));
        _mockImmichApi.Verify(api => api.GetAlbumInfoAsync(album1Id, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockImmichApi.Verify(api => api.GetAlbumInfoAsync(excludedAlbumId, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LoadAssets_NoIncludedAlbums_ReturnsEmpty()
    {
        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid>());
        _mockAccountSettings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { Guid.NewGuid() });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(It.IsAny<Guid>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumResponseDto { Assets = new List<AssetResponseDto> { CreateAsset("excluded_only") } });


        var result = (await _albumAssetsPool.GetAssets(25)).ToList();
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task LoadAssets_NoExcludedAlbums_ReturnsAlbums()
    {
        var album1Id = Guid.NewGuid();
        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid> { album1Id });
        _mockAccountSettings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid>()); // Empty excluded

        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(album1Id, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumResponseDto { Assets = new List<AssetResponseDto> { CreateAsset("A") } });

        var result = (await _albumAssetsPool.GetAssets(25)).ToList();
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.Any(a => a.Id == "A"));
    }

    [Test]
    public async Task LoadAssets_NullAlbums_ReturnsEmpty()
    {
        _mockAccountSettings.SetupGet(s => s.Albums).Returns((List<Guid>)null);

        var result = (await _albumAssetsPool.GetAssets(25)).ToList();
        Assert.That(result, Is.Empty);

        // the absence of an error, whereas before a null pointer exception would be thrown, indicates success.
    }

    [Test]
    public async Task LoadAssets_NullExcludedAlbums_Succeeds()
    {
        _mockAccountSettings.SetupGet(s => s.ExcludedAlbums).Returns((List<Guid>)null);

        var result = (await _albumAssetsPool.GetAssets(25)).ToList();
        Assert.That(result, Is.Empty);

        // the absence of an error, whereas before a null pointer exception would be thrown, indicates success.
    }

    [Test]
    public async Task LoadAssets_InvalidIncludedAlbum_SkipsAlbumAndReturnsValidAlbumAssets()
    {
        var invalidAlbumId = Guid.NewGuid();
        var validAlbumId = Guid.NewGuid();
        var validAsset = CreateAsset("valid");

        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid> { invalidAlbumId, validAlbumId });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(invalidAlbumId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateApiException(400, """{"message":"Not found or no album.read access","correlationId":"abc"}"""));
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(validAlbumId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumResponseDto { Assets = new List<AssetResponseDto> { validAsset } });

        var result = (await _albumAssetsPool.GetAssets(25)).ToList();

        Assert.That(result.Select(asset => asset.Id), Is.EqualTo(new[] { "valid" }));
        VerifyWarningLogged(invalidAlbumId);
    }

    [Test]
    public async Task LoadAssets_AllIncludedAlbumsInvalid_ReturnsEmpty()
    {
        var invalidAlbumId = Guid.NewGuid();

        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid> { invalidAlbumId });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(invalidAlbumId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateApiException(404, """{"message":"Not found"}"""));

        var result = (await _albumAssetsPool.GetAssets(25)).ToList();

        Assert.That(result, Is.Empty);
        VerifyWarningLogged(invalidAlbumId);
    }

    [Test]
    public async Task LoadAssets_InvalidExcludedAlbum_DoesNotBlockIncludedAssets()
    {
        var albumId = Guid.NewGuid();
        var excludedAlbumId = Guid.NewGuid();
        var asset = CreateAsset("included");

        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid> { albumId });
        _mockAccountSettings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { excludedAlbumId });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(albumId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlbumResponseDto { Assets = new List<AssetResponseDto> { asset } });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(excludedAlbumId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateApiException(400, """{"message":"Not found or no album.read access"}"""));

        var result = (await _albumAssetsPool.GetAssets(25)).ToList();

        Assert.That(result.Select(resultAsset => resultAsset.Id), Is.EqualTo(new[] { "included" }));
        VerifyWarningLogged(excludedAlbumId);
    }

    [Test]
    public void LoadAssets_UnexpectedAlbumApiException_Propagates()
    {
        var albumId = Guid.NewGuid();

        _mockAccountSettings.SetupGet(s => s.Albums).Returns(new List<Guid> { albumId });
        _mockImmichApi.Setup(api => api.GetAlbumInfoAsync(albumId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateApiException(500, """{"message":"server error"}"""));

        Assert.ThrowsAsync<ApiException>(async () => await _albumAssetsPool.GetAssets(25));
    }

    private static ApiException CreateApiException(int statusCode, string response)
    {
        return new ApiException("Unexpected status code.", statusCode, response, new Dictionary<string, IEnumerable<string>>(), null);
    }

    private void VerifyWarningLogged(Guid albumId)
    {
        _mockLogger.Verify(
            logger => logger.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(albumId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
