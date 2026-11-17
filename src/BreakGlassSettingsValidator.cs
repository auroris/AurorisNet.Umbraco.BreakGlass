using System.Net.Mail;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace BreakGlass;

/// <summary>
/// Stops a site from starting with settings that would quietly fail to recover the administrator. The password is
/// not checked here: Umbraco's password rules decide, and a rejected password is logged and leaves the account alone.
/// </summary>
internal sealed class BreakGlassSettingsValidator(IOptionsMonitor<SecuritySettings> security) : IValidateOptions<BreakGlassSettings>
{
    public ValidateOptionsResult Validate(string? name, BreakGlassSettings settings)
    {
        var failures = new List<string>();
        if (settings.GetAdminEmail() is { } email && !MailAddress.TryCreate(email, out _))
            failures.Add($"{BreakGlassSettings.AdminEmailKey} must be an email address, or empty to leave the administrator's email alone.");
        // A username that is not an email address cannot be typed into the login form while usernames are emails,
        // so applying it would lock the administrator out instead of letting them in.
        if (security.CurrentValue.UsernameIsEmail && settings.AdminUserName?.Trim() is { Length: > 0 } username
            && !MailAddress.TryCreate(username, out _))
            failures.Add($"{BreakGlassSettings.AdminUserNameKey} must be an email address while Umbraco:CMS:Security:UsernameIsEmail is true. Leave it out to keep the administrator's username.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
