namespace BreakGlass;

/// <summary>
/// The <c>BreakGlass</c> settings section. Every setting is read again when configuration reloads, so saving
/// <c>appsettings.json</c> applies a change to a running site without a restart.
/// </summary>
public sealed class BreakGlassSettings
{
    /// <summary>The configuration section that holds these settings.</summary>
    public const string SectionName = "BreakGlass";

    /// <summary>The full key of <see cref="DebugSignIn"/>.</summary>
    public const string DebugSignInKey = SectionName + ":" + nameof(DebugSignIn);

    /// <summary>The full key of <see cref="AdminPassword"/>.</summary>
    public const string AdminPasswordKey = SectionName + ":" + nameof(AdminPassword);

    /// <summary>The full key of <see cref="AdminUserName"/>.</summary>
    public const string AdminUserNameKey = SectionName + ":" + nameof(AdminUserName);

    /// <summary>The full key of <see cref="AdminEmail"/>.</summary>
    public const string AdminEmailKey = SectionName + ":" + nameof(AdminEmail);

    /// <summary>The administrator's username when <see cref="AdminUserName"/> is left out and usernames are not email addresses.</summary>
    public const string DefaultAdminUserName = "admin";

    /// <summary>
    /// Accepts any nonempty password for an existing backoffice account, bypassing lockout and two-factor checks, but
    /// only while <c>Umbraco:CMS:Hosting:Debug</c> is true. It is on by default, so a developer can sign in to a copy
    /// of a site without knowing its passwords; outside debug mode it has no effect.
    /// </summary>
    public bool DebugSignIn { get; set; } = true;

    /// <summary>
    /// The built-in administrator's password. A valid value is applied at startup and on every configuration reload,
    /// and also unlocks the account, clears its failed attempts and removes its two-factor enrolment, so nothing
    /// stands between this password and the account. Empty or missing leaves the account alone. The value is used
    /// exactly as written, including leading and trailing spaces.
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>
    /// The built-in administrator's username. Empty leaves it alone. Left out, it is <c>admin</c> when
    /// <c>Umbraco:CMS:Security:UsernameIsEmail</c> is false, and left alone when usernames are email addresses,
    /// because Umbraco's login form then asks for an email address.
    /// </summary>
    public string? AdminUserName { get; set; }

    /// <summary>The built-in administrator's email address. Empty or missing leaves it alone.</summary>
    public string? AdminEmail { get; set; }

    /// <summary>
    /// Whether debug sign-in applies now. Debug mode is required whatever <see cref="DebugSignIn"/> says, so a
    /// production site can never accept arbitrary passwords through this setting alone.
    /// </summary>
    /// <param name="hostingDebug">The current value of <c>Umbraco:CMS:Hosting:Debug</c>.</param>
    public bool IsDebugSignInActive(bool hostingDebug) => hostingDebug && DebugSignIn;

    /// <summary>The username to give the administrator, or null to leave it alone.</summary>
    /// <param name="usernameIsEmail">The current value of <c>Umbraco:CMS:Security:UsernameIsEmail</c>.</param>
    public string? GetAdminUserName(bool usernameIsEmail) => AdminUserName is null
        ? usernameIsEmail ? null : DefaultAdminUserName
        : AdminUserName.Trim() is { Length: > 0 } username ? username : null;

    /// <summary>The email address to give the administrator, or null to leave it alone.</summary>
    public string? GetAdminEmail() => AdminEmail?.Trim() is { Length: > 0 } email ? email : null;
}
