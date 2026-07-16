using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using ImmichFrame.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers;

[ApiController]
[Route("api/frame-sessions")]
[Authorize(AuthenticationSchemes = AuthSchemes.Frame)]
public class FrameSessionsController : ControllerBase
{
    private readonly ILogger<FrameSessionsController> _logger;
    private readonly IFrameSessionRegistry _registry;
    private readonly string? _authenticationSecret;

    public FrameSessionsController(
        ILogger<FrameSessionsController> logger,
        IFrameSessionRegistry registry,
        IGeneralSettings generalSettings)
    {
        _logger = logger;
        _registry = registry;
        _authenticationSecret = generalSettings.AuthenticationSecret;
    }

    [HttpPut("{clientIdentifier}")]
    public IActionResult UpsertSessionSnapshot(string clientIdentifier, [FromBody] FrameSessionSnapshotDto snapshot)
    {
        var sanitizedClientIdentifier = clientIdentifier.SanitizeString();
        if (string.IsNullOrWhiteSpace(sanitizedClientIdentifier))
        {
            return BadRequest("A valid client identifier is required.");
        }

        _logger.LogDebug("Frame session snapshot received for '{clientIdentifier}'", sanitizedClientIdentifier);
        _registry.UpsertSnapshot(sanitizedClientIdentifier, snapshot, Request.Headers.UserAgent.ToString());
        return NoContent();
    }

    [HttpGet("{clientIdentifier}/commands")]
    public ActionResult<IReadOnlyList<AdminCommandDto>> GetPendingCommands(string clientIdentifier)
    {
        var sanitizedClientIdentifier = clientIdentifier.SanitizeString();
        if (string.IsNullOrWhiteSpace(sanitizedClientIdentifier))
        {
            return BadRequest("A valid client identifier is required.");
        }

        var commands = _registry.GetPendingCommands(sanitizedClientIdentifier);
        return Ok(commands);
    }

    [HttpPost("{clientIdentifier}/commands/{commandId:long}/ack")]
    public IActionResult AcknowledgeCommand(string clientIdentifier, long commandId)
    {
        var sanitizedClientIdentifier = clientIdentifier.SanitizeString();
        if (string.IsNullOrWhiteSpace(sanitizedClientIdentifier))
        {
            return BadRequest("A valid client identifier is required.");
        }

        return _registry.AcknowledgeCommand(sanitizedClientIdentifier, commandId)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{clientIdentifier}/client-log")]
    public IActionResult ReportClientLog(string clientIdentifier, [FromBody] ClientLogRequest request)
    {
        var sanitizedClientIdentifier = clientIdentifier.SanitizeString();
        if (string.IsNullOrWhiteSpace(sanitizedClientIdentifier))
        {
            return BadRequest("A valid client identifier is required.");
        }

        var message = SanitizeLogText(request.Message);
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest("A log message is required.");
        }

        var details = SanitizeLogText(request.Details, maxLength: 2000);
        var level = request.Level?.Trim().ToLowerInvariant();

        // Client-reported events are logged at Warning regardless of the reported level so they
        // surface in `docker logs` (typically Information and up) without extra configuration —
        // this endpoint only exists so rare client-side failures (transition watchdog hangs,
        // video stalls) become visible without a browser console capture.
        _logger.LogWarning(
            "Client-reported event from '{clientIdentifier}' [{level}]: {message} {details}",
            sanitizedClientIdentifier,
            level ?? "error",
            message,
            details);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("{clientIdentifier}/disconnect")]
    public IActionResult Disconnect(string clientIdentifier, [FromBody] FrameSessionDisconnectRequest? request)
    {
        var sanitizedClientIdentifier = clientIdentifier.SanitizeString();
        if (string.IsNullOrWhiteSpace(sanitizedClientIdentifier))
        {
            return BadRequest("A valid client identifier is required.");
        }

        if (!IsDisconnectAuthorized(request?.AuthSecret))
        {
            return Unauthorized();
        }

        _logger.LogDebug("Frame session disconnect received for '{clientIdentifier}'", sanitizedClientIdentifier);
        _registry.MarkStopped(sanitizedClientIdentifier, Request.Headers.UserAgent.ToString());
        return NoContent();
    }

    private bool IsDisconnectAuthorized(string? requestAuthSecret)
    {
        if (string.IsNullOrWhiteSpace(_authenticationSecret))
        {
            return true;
        }

        var authorizationHeader = Request.Headers.Authorization.ToString();
        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorizationHeader["Bearer ".Length..].Trim();
            return string.Equals(token, _authenticationSecret, StringComparison.Ordinal);
        }

        return string.Equals(requestAuthSecret, _authenticationSecret, StringComparison.Ordinal);
    }

    private static string? SanitizeLogText(string? value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var singleLine = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length > maxLength ? singleLine[..maxLength] : singleLine;
    }
}
