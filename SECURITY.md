# Security

## Report a vulnerability

Report a vulnerability in private, with
[GitHub's vulnerability report form](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/security/advisories/new).
You also reach it from this repository's **Security** tab, under **Report a vulnerability**.
Please keep the details out of public issues until a fix is published.

A report is easier to act on when it includes:

- the package version and the site's Umbraco release;
- the settings involved, without real passwords;
- the steps that show the problem, and what someone gains by it.

## Versions that get fixes

BreakGlass supports Umbraco 17 and 18 on .NET 10. Its NuGet dependency range is
`[17.0.0, 19.0.0)`; earlier and later CMS majors are outside the supported range.

A fix is published as a new package version. Earlier versions stay as they are, so install
the newest version for your site; a `1.*` reference picks it up on the next restore.

The [compatibility tests](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/compatibility.md)
include older Umbraco baselines with known dependency advisories. Passing those tests
confirms BreakGlass behavior on those versions; it does not resolve the underlying advisories.

## What the package covers

The package is meant to open the backoffice to whoever controls the site's configuration,
so some access is by design:

- Anyone who can write `appsettings.json`, or supply matching environment variables when
  Umbraco starts, can choose the built-in administrator's password. Protect the
  configuration as you would the password itself.
- `BreakGlass:DebugSignIn` defaults to `false`. While it and `Umbraco:CMS:Hosting:Debug`
  are both explicitly true, anyone who can reach the backoffice login and knows an existing
  enabled account's username can sign in as that account, including an administrator, with
  any nonempty password. This bypasses lockout and two-factor checks without changing the
  stored password or two-factor enrolment.

A vulnerability is anything beyond that: debug sign-in applying while debug mode is off or
`BreakGlass:DebugSignIn` is false, any sign-in to an account that does not exist or is
disabled, the configured password reaching another account, or a password written to a
log. For a vulnerability in Umbraco itself, follow
[Umbraco's security policy](https://github.com/umbraco/Umbraco-CMS/security/policy).
