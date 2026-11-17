# Umbraco Break Glass

`AurorisNet.Umbraco.BreakGlass` lets whoever controls an Umbraco 18 site's configuration get
back into its backoffice when the administrator is locked out. It provides two routes:

- **Configured administrator password.** `BreakGlass:AdminPassword` sets the built-in
  administrator's password at startup and whenever the configuration reloads. While it is
  set, each application also unlocks the account, clears its failed sign-in attempts and
  removes its two-factor enrolment.
- **Debug sign-in.** While `Umbraco:CMS:Hosting:Debug` and `BreakGlass:DebugSignIn` are both
  true, the backoffice signs in an existing account with any nonempty password and bypasses
  lockout and two-factor checks.

The package registers itself through an Umbraco composer, so installing it completes the
setup.

## Install

Add the package to a website project that runs Umbraco 18 on .NET 10:

```xml
<PackageReference Include="AurorisNet.Umbraco.BreakGlass" Version="1.*" />
```

## Settings

| Setting | Default | When it applies and what it does |
|---|---|---|
| `BreakGlass:AdminPassword` | empty | At startup and on every configuration reload. Sets the password of the built-in administrator (user ID `-1`, whatever its current name) exactly as written, through Umbraco's password rules, then unlocks the account, clears its failed attempts and removes its two-factor enrolment. Empty leaves the account alone. A password Umbraco rejects is logged and changes nothing. |
| `BreakGlass:AdminUserName` | empty | At startup and on every reload. Renames the built-in administrator to this username. Empty keeps the name Umbraco gave the account. Must be an email address while `Umbraco:CMS:Security:UsernameIsEmail` is true. |
| `BreakGlass:AdminEmail` | empty | At startup and on every reload. Must be an email address. Empty keeps the current address. |
| `BreakGlass:DebugSignIn` | `true` | On every sign-in. Allows Debug sign-in while `Umbraco:CMS:Hosting:Debug` is true. Set it to `false` on a site that keeps debug mode on for other reasons. |

A site stops at startup with a configuration error when `DebugSignIn` is something other
than `true` or `false`, when `AdminEmail` is not an email address, or when `AdminUserName` is
not one while `UsernameIsEmail` is true. A running site that reloads such a value logs the
error, leaves the administrator's credentials as they are and turns Debug sign-in off until
the value is corrected.

## Recover a locked-out account

1. Choose a route. For the configured password, add a new password to `appsettings.json`:

   ```json
   "BreakGlass": {
     "AdminPassword": "A new password that meets the site's rules"
   }
   ```

   For Debug sign-in, set `Umbraco:CMS:Hosting:Debug` to `true`.
2. Save the file. A running site applies the change at once, and a stopped site applies it
   when it starts. A value set as an environment variable, such as
   `BreakGlass__AdminPassword`, takes effect after a restart.
3. Sign in. With the configured password, use the administrator's username and that
   password. With Debug sign-in, use the username of any existing, enabled account and any
   nonempty password.
4. Repair the account in **Users**: its password, two-factor enrolment, groups or status.
5. Remove `BreakGlass:AdminPassword`, or set `Umbraco:CMS:Hosting:Debug` back to `false`,
   and save. While `AdminPassword` stays set, every configuration reload restores that
   password and removes the administrator's two-factor enrolment, so set the
   administrator's own password and enrol two-factor after removing it.

## Security

- **Debug sign-in works only in debug mode.** It applies while `Umbraco:CMS:Hosting:Debug`
  **and** `BreakGlass:DebugSignIn` are both true, and never outside debug mode, whatever
  `DebugSignIn` says. Both are read on every sign-in, so saving debug mode as `false` closes
  it without a restart. The environment name (`Development`, `Production`) plays no part.
- **While Debug sign-in applies, anyone who reaches the backoffice login can sign in as any
  account whose username they know.** Keep debug mode off on a reachable site except during
  a recovery. Unknown usernames, empty passwords and disabled accounts are still refused.
  Each sign-in unlocks a locked account, logs a warning and leaves the account's password
  and two-factor enrolment as they are.
- **The configuration is a credential.** Whoever can write `appsettings.json` or the site's
  environment variables can set the administrator's password. On a shared server, supply
  `BreakGlass__AdminPassword` from an environment variable or secret store and remove it
  after use. No log message contains the password.
- **The package replaces Umbraco's backoffice sign-in manager**, including one the site
  registers itself, and puts its password checker in front of the site's
  `IBackOfficeUserPasswordChecker`. Outside Debug sign-in, both pass every call to Umbraco's
  implementation or the site's checker unchanged.

Report a vulnerability as the
[security policy](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/SECURITY.md)
describes.

## Developing

The repository builds with the .NET 10 SDK that `global.json` selects. The tests start real
Umbraco sites on SQLite in a temporary folder, one at a time, in about a minute.

```powershell
dotnet build -c Release
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release
dotnet pack src/AurorisNet.Umbraco.BreakGlass.csproj -c Release -o artifacts/packages
```

The package compiles against `UmbracoMinimumVersion` (18.0.0) and accepts any Umbraco 18
release; the tests run on `UmbracoVersion`, both in `Directory.Build.props`. Add
`-p:UmbracoVersion=18.0.0` to test against the oldest release. A local package is numbered
`0.0.0-local`. Render `docs/images/icon.png` from `icon.svg` at 128 by 128 pixels.

To publish, run **Actions → Release → Run workflow** on `main` with a semantic version such
as `1.0.1`. The workflow builds with warnings as errors, runs the tests, packs, tags
`v<version>`, pushes to nuget.org through trusted publishing as `AurorisNet` and creates the
GitHub release. **dry-run** stops after packing and keeps the package as an artifact for
seven days. A version with a suffix, such as `1.1.0-rc.1`, is published as a prerelease.

## License

[MIT](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/LICENSE)
