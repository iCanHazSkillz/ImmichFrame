using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(
        IWritableEffectiveSettingsProvider settingsProvider,
        BootstrapServerSettingsHolder bootstrapSettingsHolder,
        ICustomCssValidator customCssValidator,
        IFrameSessionRegistry frameSessionRegistry,
        ILogger<AdminSettingsController> logger)
    {
        _settingsProvider = settingsProvider;
        _bootstrapSettingsHolder = bootstrapSettingsHolder;
        _customCssValidator = customCssValidator;
        _frameSessionRegistry = frameSessionRegistry;
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
}
