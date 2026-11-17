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


    /// <summary>
    /// Accepts any nonempty password for an existing backoffice account, bypassing lockout and two-factor checks, but
    /// only while <c>Umbraco:CMS:Hosting:Debug</c> is true. Defaults to false and must be explicitly enabled.
    /// Anyone who can reach the login can then sign in as any enabled account, including an administrator,
    /// without knowing its password or providing a two-factor code. Outside debug mode it has no effect.
    /// </summary>
    public bool DebugSignIn { get; set; } = false;

    /// <summary>
    /// The built-in administrator's password. A valid value is applied at startup and on every configuration reload,
    /// and also unlocks the account, clears its failed attempts and removes its two-factor enrolment, so nothing
    /// stands between this password and the account. Empty or missing leaves the account alone. The value is used
    /// exactly as written, including leading and trailing spaces.
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>
    /// The built-in administrator's username. Empty or missing leaves it alone, so the account keeps the name Umbraco
    /// gave it. While <c>Umbraco:CMS:Security:UsernameIsEmail</c> is true it must be an email address, because Umbraco's
    /// login form then asks for one.
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
    public string? GetAdminUserName() => AdminUserName?.Trim() is { Length: > 0 } username ? username : null;

    /// <summary>The email address to give the administrator, or null to leave it alone.</summary>
    public string? GetAdminEmail() => AdminEmail?.Trim() is { Length: > 0 } email ? email : null;
}
