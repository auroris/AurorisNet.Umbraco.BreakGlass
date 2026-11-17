using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Security;

namespace BreakGlass.Tests;

/// <summary>The settings, their validation and the debug sign-in gate, without a site.</summary>
public sealed class SettingsTests
{
    /// <summary>Without settings, debug sign-in is on and the administrator's account is left as Umbraco created it.</summary>
    [Fact]
    public void Defaults_turn_on_debug_sign_in_and_leave_the_administrator_alone()
    {
        var settings = Bind([]);
        Assert.True(settings.DebugSignIn);
        Assert.Null(settings.AdminPassword);
        Assert.Null(settings.GetAdminUserName());
        Assert.Null(settings.GetAdminEmail());
    }

    /// <summary>Empty values switch a field off; the password keeps its spaces because they can be part of it.</summary>
    [Fact]
    public void Empty_strings_leave_the_administrator_alone_and_values_are_trimmed_except_the_password()
    {
        var empty = Bind(new() { ["AdminUserName"] = "", ["AdminEmail"] = " ", ["AdminPassword"] = "" });
        Assert.Null(empty.GetAdminUserName());
        Assert.Null(empty.GetAdminEmail());
        Assert.Equal("", empty.AdminPassword);

        var set = Bind(new() { ["AdminUserName"] = " root ", ["AdminEmail"] = " root@example.test ", ["AdminPassword"] = " spaced " });
        Assert.Equal("root", set.GetAdminUserName());
        Assert.Equal("root@example.test", set.GetAdminEmail());
        Assert.Equal(" spaced ", set.AdminPassword);
    }

    /// <summary>Debug mode is required, so the switch alone can never open a production site.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Debug_sign_in_needs_debug_mode_whatever_the_switch_says(bool hostingDebug, bool debugSignIn, bool active)
        => Assert.Equal(active, new BreakGlassSettings { DebugSignIn = debugSignIn }.IsDebugSignInActive(hostingDebug));

    /// <summary>The checker agrees with the sign-in manager, and hands every other check to the site's own checker.</summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task The_password_checker_accepts_any_nonempty_password_only_while_debug_sign_in_applies(
        bool hostingDebug, bool debugSignIn, bool emptyPassword, bool accepted)
    {
        var gate = Gate(hostingDebug, new BreakGlassSettings { DebugSignIn = debugSignIn });
        var user = BackOfficeIdentityUser.CreateNew(new GlobalSettings(), "admin", "admin@example.test", "en", "Administrator");
        var password = emptyPassword ? "" : "Not the stored password";

        Assert.Equal(accepted ? BackOfficeUserPasswordCheckerResult.ValidCredentials : BackOfficeUserPasswordCheckerResult.FallbackToDefaultChecker,
            await new DebugPasswordChecker(gate, inner: null).CheckPasswordAsync(user, password));
        // A checker the site registered decides whenever debug sign-in does not.
        var inner = new FixedChecker(BackOfficeUserPasswordCheckerResult.InvalidCredentials);
        Assert.Equal(accepted ? BackOfficeUserPasswordCheckerResult.ValidCredentials : BackOfficeUserPasswordCheckerResult.InvalidCredentials,
            await new DebugPasswordChecker(gate, inner).CheckPasswordAsync(user, password));
        Assert.Equal(accepted ? 0 : 1, inner.Calls);
    }

    /// <summary>Settings that cannot be read close debug sign-in rather than leave it in an unknown state.</summary>
    [Fact]
    public void Invalid_settings_leave_debug_sign_in_off()
    {
        var settings = new ThrowingMonitor<BreakGlassSettings>(new OptionsValidationException(Options.DefaultName, typeof(BreakGlassSettings), ["invalid"]));
        var gate = new DebugSignIn(new FixedMonitor<HostingSettings>(new HostingSettings { Debug = true }), settings, NullLogger<DebugSignIn>.Instance);
        Assert.False(gate.IsActive);
    }

    /// <summary>The site refuses to start with an email or username that would fail to recover the administrator.</summary>
    [Theory]
    [InlineData(false, null, null, true)]
    [InlineData(false, "admin", "admin@example.test", true)]
    [InlineData(false, "", "", true)]
    [InlineData(false, null, "not an address", false)]
    [InlineData(true, null, null, true)]
    [InlineData(true, "", null, true)]
    [InlineData(true, "admin@example.test", null, true)]
    [InlineData(true, "admin", null, false)]
    public void Startup_validation_rejects_an_identity_that_would_lock_the_administrator_out(
        bool usernameIsEmail, string? username, string? email, bool valid)
    {
        var validator = new BreakGlassSettingsValidator(new FixedMonitor<SecuritySettings>(new SecuritySettings { UsernameIsEmail = usernameIsEmail }));
        var result = validator.Validate(Options.DefaultName, new BreakGlassSettings { AdminUserName = username, AdminEmail = email });
        Assert.Equal(valid, result.Succeeded);
    }

    /// <summary>A switch that is not a boolean fails when the settings are bound, which ValidateOnStart does at startup.</summary>
    [Fact]
    public void A_setting_that_is_not_a_boolean_fails_at_startup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [BreakGlassSettings.DebugSignInKey] = "maybe" }).Build());
        services.AddOptions<BreakGlassSettings>().BindConfiguration(BreakGlassSettings.SectionName);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<BreakGlassSettings>>().Value);
    }

    private static BreakGlassSettings Bind(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => BreakGlassSettings.SectionName + ":" + pair.Key, pair => pair.Value))
            .Build().GetSection(BreakGlassSettings.SectionName).Get<BreakGlassSettings>() ?? new BreakGlassSettings();

    private static DebugSignIn Gate(bool hostingDebug, BreakGlassSettings settings)
        => new(new FixedMonitor<HostingSettings>(new HostingSettings { Debug = hostingDebug }),
            new FixedMonitor<BreakGlassSettings>(settings), NullLogger<DebugSignIn>.Instance);

    private sealed class FixedMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class ThrowingMonitor<T>(Exception exception) : IOptionsMonitor<T>
    {
        public T CurrentValue => throw exception;
        public T Get(string? name) => throw exception;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FixedChecker(BackOfficeUserPasswordCheckerResult result) : IBackOfficeUserPasswordChecker
    {
        public int Calls { get; private set; }

        public Task<BackOfficeUserPasswordCheckerResult> CheckPasswordAsync(BackOfficeIdentityUser user, string password)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }
}
