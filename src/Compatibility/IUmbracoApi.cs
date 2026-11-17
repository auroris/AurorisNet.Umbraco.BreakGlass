using System.Security.Claims;
using Umbraco.Cms.Core.Security;

namespace BreakGlass.Compatibility;

// Only the CMS operations used by recovery and the native sign-in entry point.
internal interface IUmbracoApi
{
    int MajorVersion { get; }
    Task ApplyAdministratorAsync(string? username, string? email, string? password);
    Task<bool> PrepareDebugSignInAsync(BackOfficeIdentityUser user);
    void NotifyLoginSuccess(ClaimsPrincipal principal, string userId);
}
