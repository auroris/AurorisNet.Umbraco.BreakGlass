using System.Net;
using BreakGlass.Compatibility;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Security;

namespace BreakGlass.Tests;

/// <summary>
/// Runs both features on real sites through the backoffice login endpoint, changing the settings by saving
/// <c>appsettings.json</c> as an administrator would. Umbraco keeps static state, so the tests of one class, which
/// xunit runs one at a time, hold every site.
/// </summary>
public sealed class SiteTests
{
    private const string FirstPassword = "First-configured-password91!";
    private const string ChangedPassword = "  Changed-configured-password92!  ";
    private const string RenamedAdministrator = "recovered@example.test";
    private const string AnyPassword = "Not the administrator's password";

    /// <summary>The password applies at startup and on every save, recovering a locked account with two-factor enrolment.</summary>
    [Fact]
    public async Task Configured_password_applies_at_startup_and_when_the_file_is_saved()
    {
        var root = TestSite.CreateRoot();
        try
        {
            await TestSite.WriteSettingsAsync(root, new { BreakGlass = new { AdminPassword = FirstPassword } });
            await using var site = await TestSite.StartAsync(root, TestContext.Current.CancellationToken);

            // Startup replaces the installed password, and the administrator signs in with the configured one.
            await TestSite.WaitUntilAsync(() => site.HasPasswordAsync(FirstPassword), "the configured password at startup");
            Assert.Equal(HttpStatusCode.OK, await site.LoginAsync(TestSite.Email, FirstPassword));
            Assert.Equal(HttpStatusCode.Unauthorized, await site.LoginAsync(TestSite.Email, TestSite.InstalledPassword));
            // Usernames are email addresses on a default site, so the username is left alone.
            Assert.Equal(TestSite.Email, (await site.AdministratorAsync()).UserName);

            // A locked account with two-factor enrolment is recovered by saving a new password.
            for (var attempt = 0; attempt < 6; attempt++)
                Assert.Contains(await site.LoginAsync(TestSite.Email, AnyPassword), new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
            Assert.True((await site.LockoutAsync()).Locked);
            await site.EnrollTwoFactorAsync();
            Assert.Equal(TestTwoFactorProvider.Name, await site.TwoFactorProvidersAsync());
            await TestSite.WriteSettingsAsync(root, new { BreakGlass = new { AdminPassword = ChangedPassword } });
            await TestSite.WaitUntilAsync(() => site.HasPasswordAsync(ChangedPassword), "the saved password");
            await TestSite.WaitUntilAsync(async () => await site.LockoutAsync() == (false, 0), "the lockout to be lifted");
            await TestSite.WaitUntilAsync(async () => await site.TwoFactorProvidersAsync() == "", "two-factor enrolment to be removed");
            // The password is used exactly as written, spaces included.
            Assert.False(await site.HasPasswordAsync(ChangedPassword.Trim()));
            Assert.Equal(HttpStatusCode.OK, await site.LoginAsync(TestSite.Email, ChangedPassword));
            var changed = (await site.AdministratorAsync()).SecurityStamp;

            // A rejected password is logged and leaves the account alone, and so does an empty one.
            var errors = site.Log.Errors.Count;
            await TestSite.WriteSettingsAsync(root, new { BreakGlass = new { AdminPassword = "short" } });
            await TestSite.WaitUntilAsync(() => Task.FromResult(site.Log.Errors.Count > errors), "the rejected password to be logged");
            await TestSite.WriteSettingsAsync(root, new { BreakGlass = new { AdminPassword = "" } });
            await Task.Delay(1500, TestContext.Current.CancellationToken);
            Assert.True(await site.HasPasswordAsync(ChangedPassword));
            Assert.Equal(changed, (await site.AdministratorAsync()).SecurityStamp);

            // The username and email apply on reload too, and the administrator signs in with the new name.
            await TestSite.WriteSettingsAsync(root, new
            {
                BreakGlass = new { AdminPassword = ChangedPassword, AdminUserName = RenamedAdministrator, AdminEmail = RenamedAdministrator },
            });
            await TestSite.WaitUntilAsync(async () => (await site.AdministratorAsync()) is { UserName: RenamedAdministrator, Email: RenamedAdministrator },
                "the administrator to be renamed");
            Assert.Equal(HttpStatusCode.OK, await site.LoginAsync(RenamedAdministrator, ChangedPassword));
            Assert.True(await site.HasPasswordAsync(ChangedPassword));

            foreach (var password in new[] { TestSite.InstalledPassword, FirstPassword, ChangedPassword, "short" })
                Assert.DoesNotContain(site.Log.Messages, message => message.Contains(password, StringComparison.Ordinal));
        }
        finally { TestSite.DeleteRoot(root); }
    }

    /// <summary>Debug sign-in opens a locked account with any password, and saving the settings closes and reopens it.</summary>
    [Fact]
    public async Task Debug_sign_in_needs_debug_mode_and_its_switch_while_normal_passwords_keep_working()
    {
        var root = TestSite.CreateRoot();
        try
        {
            await TestSite.WriteSettingsAsync(root, Settings(debug: true, debugSignIn: null));
            await using var site = await TestSite.StartAsync(root, TestContext.Current.CancellationToken);
            using (var scope = site.Services.CreateScope())
            {
                var major = scope.ServiceProvider.GetRequiredService<IUmbracoVersion>().Version.Major;
                var api = scope.ServiceProvider.GetRequiredService<IUmbracoApi>();
                Assert.Equal(major, api.MajorVersion);
                Assert.Equal(major == 17 ? typeof(Umbraco17Api) : typeof(Umbraco18Api), api.GetType());
                Assert.IsType<DebugBackOfficeSignInManager>(scope.ServiceProvider.GetRequiredService<Umbraco.Cms.Web.Common.Security.IBackOfficeSignInManager>());
                Assert.IsType<DebugBackOfficeSignInManager>(scope.ServiceProvider.GetRequiredService<ICoreBackOfficeSignInManager>());
                Assert.IsType<DebugPasswordChecker>(scope.ServiceProvider.GetRequiredService<IBackOfficeUserPasswordChecker>());
            }

            // Debug mode alone does not enable the bypass: the setting must be explicitly true.
            Assert.False(site.Services.GetRequiredService<IOptionsMonitor<BreakGlassSettings>>().CurrentValue.DebugSignIn);
            await AssertNormalSignInAsync(site, HttpStatusCode.OK);
            await TestSite.WriteSettingsAsync(root, Settings(debug: true, debugSignIn: true));
            await TestSite.WaitUntilAsync(() => Task.FromResult(site.Services.GetRequiredService<IOptionsMonitor<BreakGlassSettings>>().CurrentValue.DebugSignIn),
                "debug sign-in to be explicitly enabled");

            // Once enabled, any nonempty password works for an existing account, past lockout and two-factor.
            await site.EnrollTwoFactorAsync();
            await LockAsync(site);
            var before = await site.AdministratorAsync();
            Assert.Equal(HttpStatusCode.OK, await site.LoginAsync(TestSite.Email, AnyPassword));
            var after = await site.AdministratorAsync();
            Assert.Equal((false, 0), await site.LockoutAsync());
            Assert.NotEqual(before.LastLoginDate, after.LastLoginDate);
            // Debug sign-in changes neither the credentials nor the enrolment.
            Assert.Equal(before.PasswordHash, after.PasswordHash);
            Assert.Equal(TestTwoFactorProvider.Name, await site.TwoFactorProvidersAsync());
            Assert.NotEqual(HttpStatusCode.OK, await site.LoginAsync(TestSite.Email, ""));
            Assert.NotEqual(HttpStatusCode.OK, await site.LoginAsync("nobody@example.test", AnyPassword));

            // The switch turns it off, and the real password works as usual.
            await TestSite.WriteSettingsAsync(root, Settings(debug: true, debugSignIn: false));
            await TestSite.WaitUntilAsync(() => Task.FromResult(!site.Services.GetRequiredService<IOptionsMonitor<BreakGlassSettings>>().CurrentValue.DebugSignIn),
                "the switch to be read");
            // Two-factor enrolment is asked for again; once it is removed, the real password signs in.
            await AssertNormalSignInAsync(site, HttpStatusCode.PaymentRequired);
            await site.DisableTwoFactorAsync();
            await AssertNormalSignInAsync(site, HttpStatusCode.OK);

            // Outside debug mode the switch has no effect.
            await TestSite.WriteSettingsAsync(root, Settings(debug: false, debugSignIn: true));
            await TestSite.WaitUntilAsync(() => Task.FromResult(!site.Services.GetRequiredService<IOptionsMonitor<HostingSettings>>().CurrentValue.Debug),
                "debug mode to be switched off");
            Assert.True(site.Services.GetRequiredService<IOptionsMonitor<BreakGlassSettings>>().CurrentValue.DebugSignIn);
            await AssertNormalSignInAsync(site, HttpStatusCode.OK);

            // Saving debug mode back on restores explicitly enabled debug sign-in without a restart.
            await TestSite.WriteSettingsAsync(root, Settings(debug: true, debugSignIn: true));
            await TestSite.WaitUntilAsync(() => Task.FromResult(site.Services.GetRequiredService<IOptionsMonitor<HostingSettings>>().CurrentValue.Debug),
                "debug mode to be switched on");
            Assert.Equal(HttpStatusCode.OK, await site.LoginAsync(TestSite.Email, AnyPassword));

            // Removing the setting restores normal authentication even while debug mode stays on.
            await TestSite.WriteSettingsAsync(root, Settings(debug: true, debugSignIn: null));
            await TestSite.WaitUntilAsync(() => Task.FromResult(!site.Services.GetRequiredService<IOptionsMonitor<BreakGlassSettings>>().CurrentValue.DebugSignIn),
                "debug sign-in to default to off after removing the setting");
            await AssertNormalSignInAsync(site, HttpStatusCode.OK);
        }
        finally { TestSite.DeleteRoot(root); }
    }

    private static object Settings(bool debug, bool? debugSignIn) => debugSignIn is { } enabled
        ? new { Umbraco = new { CMS = new { Hosting = new { Debug = debug } } }, BreakGlass = new { DebugSignIn = enabled } }
        : new { Umbraco = new { CMS = new { Hosting = new { Debug = debug } } } };

    /// <summary>Only the real password signs in, with the status a site without the package would return.</summary>
    private static async Task AssertNormalSignInAsync(TestSite site, HttpStatusCode realPassword)
    {
        using (var scope = site.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
            var administrator = (await users.FindByIdAsync(Umbraco.Cms.Core.Constants.Security.SuperUserKey.ToString()))!;
            Assert.False(await users.CheckPasswordAsync(administrator, AnyPassword));
            Assert.True(await users.CheckPasswordAsync(administrator, TestSite.InstalledPassword));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await site.LoginAsync(TestSite.Email, AnyPassword));
        Assert.Equal(realPassword, await site.LoginAsync(TestSite.Email, TestSite.InstalledPassword));
        // Clear the failed attempt, so the next check starts from the same state.
        using (var scope = site.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
            Assert.True((await users.ResetAccessFailedCountAsync((await users.FindByIdAsync(Umbraco.Cms.Core.Constants.Security.SuperUserKey.ToString()))!)).Succeeded);
        }
    }

    private static async Task LockAsync(TestSite site)
    {
        using var scope = site.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
        var administrator = (await users.FindByIdAsync(Umbraco.Cms.Core.Constants.Security.SuperUserKey.ToString()))!;
        Assert.True((await users.SetLockoutEndDateAsync(administrator, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
        Assert.True((await site.LockoutAsync()).Locked);
    }
}
