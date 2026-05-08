using Microsoft.ApplicationInsights;
using MinionTank.Auth;

namespace MinionTank.Services;

/// <summary>
/// Structured audit log emitter. Writes to ILogger and to App Insights as a custom event.
/// One line per write action so we can reconstruct mass-action incidents post-hoc (Moltbook F8).
/// </summary>
public sealed class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly TelemetryClient? _telemetry;

    public AuditLogger(ILogger<AuditLogger> logger, TelemetryClient? telemetry = null)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    public void Write(
        Principal principal,
        string action,
        string target,
        string result,
        IDictionary<string, string>? extra = null)
    {
        var props = new Dictionary<string, string>
        {
            ["principalKind"] = principal.Kind.ToString(),
            ["principalId"] = principal.Id,
            ["principalName"] = principal.DisplayName,
            ["action"] = action,
            ["target"] = target,
            ["result"] = result,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
        };

        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                props[kv.Key] = kv.Value;
            }
        }

        _logger.LogInformation(
            "AUDIT principal={Principal} action={Action} target={Target} result={Result}",
            $"{principal.Kind}:{principal.Id}",
            action,
            target,
            result);

        _telemetry?.TrackEvent("MinionTank.Audit", props);
    }
}
