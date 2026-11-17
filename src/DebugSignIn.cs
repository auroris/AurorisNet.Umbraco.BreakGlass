using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace BreakGlass;

/// <summary>Decides, for each sign-in, whether debug sign-in applies, from the current settings.</summary>
internal sealed class DebugSignIn(
    IOptionsMonitor<HostingSettings> hosting,
    IOptionsMonitor<BreakGlassSettings> settings,
    ILogger<DebugSignIn> logger)
{
    public bool IsActive
    {
        get
        {
            try
            {
                return settings.CurrentValue.IsDebugSignInActive(hosting.CurrentValue.Debug);
            }
            // Settings saved with an error leave debug sign-in off, never on, until they are corrected.
            catch (Exception exception) when (exception is OptionsValidationException or InvalidOperationException)
            {
                logger.LogError(exception, "Debug sign-in is off because the {Section} settings are invalid.", BreakGlassSettings.SectionName);
                return false;
            }
        }
    }
}
