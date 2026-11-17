using Umbraco.Cms.Core.Security;

namespace BreakGlass.Compatibility;

/// <summary>
/// Accepts any nonempty password while debug sign-in applies, so that <c>CheckPasswordAsync</c> agrees with the
/// sign-in manager. Otherwise it asks the checker the site had registered before, or Umbraco's own password check.
/// </summary>
/// <remarks>
/// Umbraco consults this checker only for accounts that exist, so it never creates access for an unknown username.
/// </remarks>
internal sealed class DebugPasswordChecker(DebugSignIn debugSignIn, IBackOfficeUserPasswordChecker? inner) : IBackOfficeUserPasswordChecker
{
    public Task<BackOfficeUserPasswordCheckerResult> CheckPasswordAsync(BackOfficeIdentityUser user, string password)
        => !string.IsNullOrEmpty(password) && debugSignIn.IsActive
            ? Task.FromResult(BackOfficeUserPasswordCheckerResult.ValidCredentials)
            : inner?.CheckPasswordAsync(user, password)
                ?? Task.FromResult(BackOfficeUserPasswordCheckerResult.FallbackToDefaultChecker);
}
