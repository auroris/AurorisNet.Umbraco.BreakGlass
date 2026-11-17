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

A fix is published as a new package version. Earlier versions stay as they are, so install
the newest version for your site; a `1.*` reference picks it up on the next restore.

## What the package covers

The package is meant to open the backoffice to whoever controls the site's configuration,
so some access is by design:

- Anyone who can write `appsettings.json`, or set the matching environment variables, can
  choose the built-in administrator's password. Protect the configuration as you would the
  password itself.
- While `Umbraco:CMS:Hosting:Debug` and `BreakGlass:DebugSignIn` are both true, anyone who
  can reach the backoffice login can sign in to an existing account with any nonempty
  password.

A vulnerability is anything beyond that: debug sign-in applying while debug mode is off or
`BreakGlass:DebugSignIn` is false, any sign-in to an account that does not exist or is
disabled, the configured password reaching another account, or a password written to a
log. For a vulnerability in Umbraco itself, follow
[Umbraco's security policy](https://github.com/umbraco/Umbraco-CMS/security/policy).
