using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Security;

namespace BreakGlass.Compatibility;

// Shared Umbraco 17/18 account operations. Version adapters override only API differences.
internal abstract class UmbracoApi(IServiceProvider services) : IUmbracoApi
{
    public abstract int MajorVersion { get; }

    public virtual async Task ApplyAdministratorAsync(string? username, string? email, string? password)
    {
        var logger = services.GetRequiredService<ILogger<ConfiguredAdminPassword>>();
        var users = services.GetRequiredService<IBackOfficeUserManager>();
        // The original administrator remains the target even if its email or name changes.
        var administrator = await users.FindByIdAsync(Constants.Security.SuperUserKey.ToString());
        if (administrator is null)
        {
            logger.LogError("Could not configure the administrator credentials: the built-in administrator account was not found.");
            return;
        }

        // Apply identity and password settings in the same queue to avoid stale identity records.
        var rename = username is not null && !string.Equals(administrator.UserName, username, StringComparison.Ordinal);
        var changeEmail = email is not null && !string.Equals(administrator.Email, email, StringComparison.Ordinal);
        if (rename || changeEmail)
        {
            if (rename) administrator.UserName = username;
            if (changeEmail) administrator.Email = email;
            var renamed = await users.UpdateAsync(administrator);
            if (!renamed.Succeeded)
            {
                logger.LogError("Could not apply {UserNameSetting} and {EmailSetting}: {Errors}.",
                    BreakGlassSettings.AdminUserNameKey, BreakGlassSettings.AdminEmailKey,
                    string.Join("; ", renamed.Errors.Select(error => error.Description)));
                return;
            }
            logger.LogInformation("Administrator identity configured as {UserName}, {Email}.", administrator.UserName, administrator.Email);
            // Umbraco can change the persisted security stamp when the identity is saved.
            // Password reset tokens must use that stamp, not the pre-update instance.
            administrator = await users.FindByIdAsync(administrator.Id)
                ?? throw new InvalidOperationException("The administrator disappeared after its identity was configured.");
        }
        if (string.IsNullOrWhiteSpace(password)) return;

        // Check the stored hash directly: debug sign-in can make the password checker accept any
        // password. An unchanged password must not reset the account's security stamp.
        var hasher = services.GetRequiredService<IPasswordHasher<BackOfficeIdentityUser>>();
        var unchanged = administrator.PasswordHash is { } hash
            && hasher.VerifyHashedPassword(administrator, hash, password) != PasswordVerificationResult.Failed;
        if (!unchanged)
        {
            var token = await users.GeneratePasswordResetTokenAsync(administrator);
            var result = await users.ChangePasswordWithResetAsync(administrator.Id, token, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not apply {Setting}: {Errors}. Correct the setting and save the file to try again.",
                    BreakGlassSettings.AdminPasswordKey, string.Join("; ", result.Errors.Select(error => error.Description)));
                return;
            }

            // ChangePasswordWithResetAsync loads and saves another identity instance.
            // Reload it before recovery so no old password or security stamp can be saved back.
            administrator = await users.FindByIdAsync(administrator.Id)
                ?? throw new InvalidOperationException("The administrator disappeared after its password was reset.");
            logger.LogInformation("Administrator password applied from {Setting} for {UserName}.", BreakGlassSettings.AdminPasswordKey, administrator.UserName);
        }

        // Reapplying the same valid password can also recover a locked account, without
        // rotating its hash or stamp when an unrelated configuration change is saved.
        if (administrator.IsLockedOut)
        {
            var unlocked = await users.SetLockoutEndDateAsync(administrator, DateTimeOffset.UtcNow.AddMinutes(-1));
            if (!unlocked.Succeeded)
            {
                logger.LogError("Administrator password is configured, but unlocking failed: {Errors}.",
                    string.Join("; ", unlocked.Errors.Select(error => error.Description)));
                return;
            }
        }
        if (administrator.AccessFailedCount > 0)
        {
            var resetAttempts = await users.ResetAccessFailedCountAsync(administrator);
            if (!resetAttempts.Succeeded)
                logger.LogError("Administrator password is configured, but resetting failed login attempts failed: {Errors}.",
                    string.Join("; ", resetAttempts.Errors.Select(error => error.Description)));
        }

        // The configured password is the break-glass credential, so nothing may stand between it and
        // the account: two-factor enrolment goes too, as a lost authenticator is the other lockout.
        var twoFactor = services.GetRequiredService<ITwoFactorLoginService>();
        foreach (var provider in (await twoFactor.GetEnabledTwoFactorProviderNamesAsync(administrator.Key)).ToArray())
        {
            if (await twoFactor.DisableAsync(administrator.Key, provider))
                logger.LogInformation("Administrator password is configured; two-factor authentication with {Provider} was removed from {UserName}.", provider, administrator.UserName);
            else
                logger.LogError("Administrator password is configured, but two-factor authentication with {Provider} could not be removed.", provider);
        }
    }

    public virtual async Task<bool> PrepareDebugSignInAsync(BackOfficeIdentityUser user)
    {
        var users = services.GetRequiredService<BackOfficeUserManager>();
        var logger = services.GetRequiredService<ILogger<SignInManager<BackOfficeIdentityUser>>>();
        // Umbraco's authorize endpoint signs a locked account out again, so the lockout must be lifted.
        if (await users.IsLockedOutAsync(user)
            && !((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded
                && (await users.ResetAccessFailedCountAsync(user)).Succeeded))
        {
            logger.LogWarning("Could not lift the lockout for debug sign-in of backoffice user {UserId}.", user.Id);
            return false;
        }

        user.LastLoginDate = DateTime.UtcNow;
        if (!(await users.UpdateAsync(user)).Succeeded)
        {
            logger.LogWarning("Could not record the debug sign-in of backoffice user {UserId}.", user.Id);
            return false;
        }
        return true;
    }

    public virtual void NotifyLoginSuccess(ClaimsPrincipal principal, string userId)
        => services.GetRequiredService<BackOfficeUserManager>().NotifyLoginSuccess(principal, userId);
}
