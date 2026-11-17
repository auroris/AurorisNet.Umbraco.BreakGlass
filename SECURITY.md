# Security

## Report a vulnerability

Report a vulnerability in private, with
[GitHub's vulnerability report form](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/security/advisories/new),
also under **Security → Report a vulnerability** in this repository. Keep the details out of
public issues until a fix is published. Include:

- the package version and the site's Umbraco release;
- the settings involved, without real passwords;
- the steps that show the problem, and what someone gains by it.

## Versions that get fixes

A fix is published as a new package version, for the Umbraco releases in the
[Supported versions table](README.md#supported-versions). Install the newest package to get
it. A `1.*` reference picks it up on the next restore.

## What the package covers

The package opens the backoffice to whoever controls the site's configuration, so it gives
this access by design:

- Anyone who can write `appsettings.json`, or set the matching environment variables before
  Umbraco starts, can choose the built-in administrator's password. Protect the
  configuration as you would the password itself.
- While `BreakGlass:DebugSignIn` and `Umbraco:CMS:Hosting:Debug` are both `true`, anyone who
  can reach the backoffice login and knows an existing, enabled account's username can sign
  in as that account, administrators included, with any nonempty password. The sign-in
  passes lockout and two-factor checks and leaves the stored password and the two-factor
  enrolment unchanged. `DebugSignIn` is `false` by default.

Report here anything beyond that: Debug sign-in applying while debug mode is off or
`BreakGlass:DebugSignIn` is `false`, a sign-in to an account that does not exist or is
disabled, the configured password reaching another account, or a password written to a log.
For a vulnerability in Umbraco itself, follow
[Umbraco's security policy](https://github.com/umbraco/Umbraco-CMS/security/policy).
