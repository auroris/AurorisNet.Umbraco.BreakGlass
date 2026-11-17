using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Management.Security;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;

namespace BreakGlass.Compatibility;

/// <summary>
/// Umbraco's backoffice sign-in manager, which also signs in with any nonempty password while debug sign-in applies.
/// Otherwise every sign-in goes through Umbraco's own checks unchanged.
/// </summary>
internal sealed class DebugBackOfficeSignInManager(
    BackOfficeUserManager userManager,
    IHttpContextAccessor contextAccessor,
    IBackOfficeExternalLoginProviders externalLogins,
    IUserClaimsPrincipalFactory<BackOfficeIdentityUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    IOptions<GlobalSettings> globalSettings,
    ILogger<SignInManager<BackOfficeIdentityUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<BackOfficeIdentityUser> confirmation,
    IEventAggregator eventAggregator,
    IOptions<SecuritySettings> securitySettings,
    IOptions<BackOfficeAuthenticationTypeSettings> authenticationTypes,
    IRequestCache requestCache,
    DebugSignIn debugSignIn,
    IUmbracoApi api)
    : BackOfficeSignInManager(userManager, contextAccessor, externalLogins, claimsFactory,
        optionsAccessor, globalSettings, logger, schemes, confirmation, eventAggregator,
        securitySettings, authenticationTypes, requestCache)
{
    public override async Task<SignInResult> PasswordSignInAsync(BackOfficeIdentityUser user, string password, bool isPersistent, bool lockoutOnFailure)
    {
        if (!debugSignIn.IsActive) return await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
        if (string.IsNullOrEmpty(password)) return SignInResult.Failed;
        // A disabled or unconfirmed account stays closed: debug sign-in bypasses credentials, not account status.
        if (!user.IsApproved || !await CanSignInAsync(user)) return SignInResult.NotAllowed;

        if (!await api.PrepareDebugSignInAsync(user)) return SignInResult.Failed;

        var principal = await CreateUserPrincipalAsync(user);
        // The base SignInAsync would also update the account's security stamp. Debug sign-in leaves the account's
        // credentials as they are, while keeping the cookie handler's native login session.
        await Context.SignInAsync(AuthenticationType, principal, new AuthenticationProperties { IsPersistent = isPersistent });
        api.NotifyLoginSuccess(Context.User, user.Id);
        Logger.LogWarning("Debug sign-in for backoffice user {UserId}: Umbraco:CMS:Hosting:Debug and {Setting} are both on.",
            user.Id, BreakGlassSettings.DebugSignInKey);
        return SignInResult.Success;
    }
}
