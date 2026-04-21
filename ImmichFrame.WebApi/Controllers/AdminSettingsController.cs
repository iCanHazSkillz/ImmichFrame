using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;

namespace ImmichFrame.WebApi.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
public class AdminSettingsController : ControllerBase
{
    private readonly IWritableEffectiveSettingsProvider _settingsProvider;
    private readonly BootstrapServerSettingsHolder _bootstrapSettingsHolder;
    private readonly ICustomCssValidator _customCssValidator;
    private readonly IFrameSessionRegistry _frameSessionRegistry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(
        IWritableEffectiveSettingsProvider settingsProvider,
        BootstrapServerSettingsHolder bootstrapSettingsHolder,
        ICustomCssValidator customCssValidator,
        IFrameSessionRegistry frameSessionRegistry,
        IHttpClientFactory httpClientFactory,
        ILogger<AdminSettingsController> logger)
    {
        _settingsProvider = settingsProvider;
        _bootstrapSettingsHolder = bootstrapSettingsHolder;
        _customCssValidator = customCssValidator;
        _frameSessionRegistry = frameSessionRegistry;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<AdminSettingsResponseDto> Get()
    {
        var snapshot = _settingsProvider.GetCurrentSnapshot();
        return Ok(AdminSettingsResponseDto.FromSettings(
            snapshot.Version,
            snapshot.Settings,
            _bootstrapSettingsHolder.Settings,
            snapshot.CustomCss,
            !string.IsNullOrWhiteSpace(snapshot.Settings.GeneralSettings.WeatherApiKey),
            TimeZoneSettingsHelper.ResolveServerTimeZoneId(),
            TimeZoneSettingsHelper.GetAvailableTimeZoneIds().ToList()));
    }

    [HttpPost("albums/validate")]
    public async Task<ActionResult<AdminAlbumValidationResponseDto>> ValidateAlbums([FromBody] AdminAlbumValidationRequest request, CancellationToken ct)
    {
        request ??= new AdminAlbumValidationRequest();
        var accountIdentifier = request.AccountIdentifier?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accountIdentifier))
        {
            ModelState.AddModelError(nameof(request.AccountIdentifier), "Account identifier is required.");
            return ValidationProblem(ModelState);
        }

        var snapshot = _settingsProvider.GetCurrentSnapshot();
        var account = snapshot.Settings.Accounts
            .FirstOrDefault(account => string.Equals(
                ServerSettingsFactory.GetAccountIdentifier(account),
                accountIdentifier,
                StringComparison.Ordinal));

        if (account == null)
        {
            ModelState.AddModelError(nameof(request.AccountIdentifier), "Account identifier was not found.");
            return ValidationProblem(ModelState);
        }

        var httpClient = _httpClientFactory.CreateClient("ImmichApiAccountClient");
        httpClient.UseApiKey(account.ApiKey);
        var immichApi = new ImmichApi(account.ImmichServerUrl, httpClient);

        return Ok(new AdminAlbumValidationResponseDto
        {
            Albums = await ValidateAlbumList(immichApi, request.Albums, ct),
            ExcludedAlbums = await ValidateAlbumList(immichApi, request.ExcludedAlbums, ct)
        });
    }

    [HttpPut]
    public ActionResult<AdminSettingsResponseDto> Update([FromBody] AdminSettingsUpdateRequest request)
    {
        request ??= new AdminSettingsUpdateRequest();

        if (!TimeZoneSettingsHelper.TryResolveCalendarTimeZoneId(
                request.General.CalendarTimeZone,
                out var resolvedCalendarTimeZoneId))
        {
            ModelState.AddModelError(
                nameof(request.General.CalendarTimeZone),
                "Calendar timezone must be a valid timezone identifier.");
            return ValidationProblem(ModelState);
        }

        request.General.CalendarTimeZone = string.IsNullOrWhiteSpace(request.General.CalendarTimeZone)
            ? null
            : resolvedCalendarTimeZoneId;

        var document = request.ToDocument();

        if (document.General.ShowCalendar && (document.General.Webcalendars == null || document.General.Webcalendars.Count == 0))
        {
            ModelState.AddModelError(nameof(document.General.Webcalendars), "At least one webcalendar is required when the calendar widget is enabled.");
            return ValidationProblem(ModelState);
        }

        var existingSnapshot = _settingsProvider.GetCurrentSnapshot();
        var hasWeatherApiKey = !string.IsNullOrWhiteSpace(existingSnapshot.Settings.GeneralSettings.WeatherApiKey)
            || !string.IsNullOrWhiteSpace(request.WeatherApiKey);
        if (document.General.ShowWeather && !hasWeatherApiKey)
        {
            ModelState.AddModelError(nameof(request.WeatherApiKey), "A weather API key is required before the weather widget can be enabled.");
            return ValidationProblem(ModelState);
        }

        string sanitizedCustomCss;
        try
        {
            sanitizedCustomCss = _customCssValidator.ValidateAndSanitize(request.CustomCss);
        }
        catch (CustomCssValidationException ex)
        {
            ModelState.AddModelError(nameof(request.CustomCss), ex.Message);
            return ValidationProblem(ModelState);
        }

        var snapshot = _settingsProvider.Update(document, sanitizedCustomCss, request.WeatherApiKey);

        foreach (var session in _frameSessionRegistry.GetActiveSessions())
        {
            var enqueueResult = _frameSessionRegistry.EnqueueCommand(session.ClientIdentifier, FrameAdminCommandType.Refresh);
            if (enqueueResult.Status == FrameSessionCommandEnqueueStatus.Enqueued)
            {
                _logger.LogInformation("Queued Refresh command for frame session '{clientIdentifier}' after admin settings update.", session.ClientIdentifier);
            }
        }

        return Ok(AdminSettingsResponseDto.FromSettings(
            snapshot.Version,
            snapshot.Settings,
            _bootstrapSettingsHolder.Settings,
            snapshot.CustomCss,
            !string.IsNullOrWhiteSpace(snapshot.Settings.GeneralSettings.WeatherApiKey),
            TimeZoneSettingsHelper.ResolveServerTimeZoneId(),
            TimeZoneSettingsHelper.GetAvailableTimeZoneIds().ToList()));
    }

    private static async Task<List<AdminAlbumValidationResultDto>> ValidateAlbumList(
        ImmichApi immichApi,
        IEnumerable<Guid>? albumIds,
        CancellationToken ct)
    {
        var results = new List<AdminAlbumValidationResultDto>();
        foreach (var albumId in albumIds ?? [])
        {
            results.Add(await ValidateAlbum(immichApi, albumId, ct));
        }

        return results;
    }

    private static async Task<AdminAlbumValidationResultDto> ValidateAlbum(
        ImmichApi immichApi,
        Guid albumId,
        CancellationToken ct)
    {
        try
        {
            await immichApi.GetAlbumInfoAsync(albumId, null, true, ct);
            return new AdminAlbumValidationResultDto
            {
                AlbumId = albumId,
                Status = "valid"
            };
        }
        catch (ApiException ex) when (AssetHelper.IsExpectedAlbumLookupFailure(ex))
        {
            return new AdminAlbumValidationResultDto
            {
                AlbumId = albumId,
                Status = "notFoundOrNoAccess",
                StatusCode = ex.StatusCode,
                Message = ex.Response,
                CorrelationId = AssetHelper.TryGetCorrelationId(ex.Response)
            };
        }
        catch (ApiException ex)
        {
            return new AdminAlbumValidationResultDto
            {
                AlbumId = albumId,
                Status = "error",
                StatusCode = ex.StatusCode,
                Message = ex.Response,
                CorrelationId = AssetHelper.TryGetCorrelationId(ex.Response)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AdminAlbumValidationResultDto
            {
                AlbumId = albumId,
                Status = "error",
                Message = ex.Message
            };
        }
    }
}
