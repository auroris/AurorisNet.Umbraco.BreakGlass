# Umbraco Break Glass

`AurorisNet.Umbraco.BreakGlass` lets whoever controls an Umbraco 17 or 18 site's configuration get
back into its backoffice when the administrator is locked out.

**Recover the built-in administrator (user ID `-1`) through configuration.** Set
`BreakGlass:AdminPassword` to choose a new password, and use `BreakGlass:AdminUserName` and
`BreakGlass:AdminEmail` if the account also needs renaming. At startup and whenever the
configuration reloads, a valid configured password is applied, the account is unlocked,
failed sign-in attempts are cleared and two-factor enrolment is removed. This is the
normal break-glass recovery route.

**Debug sign-in is an optional fallback, off by default.** Enable it only if you deliberately
need the authentication bypass described below.

The package registers itself through an Umbraco composer, so installing it completes the
setup.

## Install

Add the package to a website project that runs Umbraco 17 or 18 on .NET 10:

```xml
<PackageReference Include="AurorisNet.Umbraco.BreakGlass" Version="1.*" />
```

One package supports both majors, with dependency range `[17.0.0, 19.0.0)`. It compiles
against Umbraco 17.0.0 and selects the adapter for the running site's major version.
See the [compatibility guide](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/compatibility.md)
for the tested versions, API wrappers and validation commands.

## Settings

| Setting | Default | When it applies and what it does |
|---|---|---|
| `BreakGlass:AdminPassword` | empty | At startup and on every configuration reload. Sets the password of the built-in administrator (user ID `-1`, whatever its current name) exactly as written, through Umbraco's password rules, then unlocks the account, clears its failed attempts and removes its two-factor enrolment. Empty skips password recovery. A password Umbraco rejects is logged and leaves the existing password unchanged; username and email settings are applied separately. |
| `BreakGlass:AdminUserName` | empty | At startup and on every reload. Renames the built-in administrator to this username. Empty keeps the current username. Must be an email address while `Umbraco:CMS:Security:UsernameIsEmail` is true. |
| `BreakGlass:AdminEmail` | empty | At startup and on every reload. Must be an email address. Empty keeps the current address. |
| `BreakGlass:DebugSignIn` | `false` | On every sign-in. Explicitly setting this to `true` allows any nonempty password for existing enabled accounts, bypassing lockout and two-factor checks, only while `Umbraco:CMS:Hosting:Debug` is also true. Missing or `false` keeps normal authentication. |

> [!WARNING]
> **DebugSignIn bypasses authentication for existing backoffice accounts, including administrators.**
> It defaults to `false`. If you explicitly set **both** `BreakGlass:DebugSignIn` and
> `Umbraco:CMS:Hosting:Debug` to `true`, **anyone who can reach the backoffice login and knows
> an enabled account's username can sign in as that account with any nonempty password**.
> They do not need the account's real password or a two-factor code. The sign-in also lifts
> account lockout. Unknown usernames, empty passwords and disabled accounts are still refused.

A site stops at startup with a configuration error when `DebugSignIn` is something other
than `true` or `false`, when `AdminEmail` is not an email address, or when `AdminUserName` is
not one while `UsernameIsEmail` is true. If a running site reloads invalid BreakGlass
settings, the plugin logs the error, applies no administrator changes and disables Debug
sign-in until the configuration is corrected. Normal sign-in continues, and previously
applied account changes are not undone.

A password rejected by Umbraco's password rules is handled separately: the rejection is
logged and the existing password is retained. It does not disable Debug sign-in, and valid
username or email changes may already have applied.

## Recover a locked-out account

1. **Reset the administrator through the configuration file.** Add a new password to
   `appsettings.json`:

   ```json
   "BreakGlass": {
     "AdminPassword": "A new password that meets the site's rules"
   }
   ```

   Add `AdminUserName` and `AdminEmail` to the same section if they also need changing.
   This targets the built-in administrator (ID `-1`), whatever its current username.
   Save the file. A running site applies the change when the file reloads; a stopped site
   applies it when Umbraco starts.
2. **Log in as your administrator.** Use the configured password and the account's username,
   including the new username if you changed it.
3. **Remove the recovery settings, then finish securing the account.** Remove `AdminPassword`
   and any `AdminUserName` or `AdminEmail` values you added from the `BreakGlass` section,
   and save the file. After the configuration reloads, change your administrator password,
   re-enrol in two-factor authentication and adjust any other account settings as needed.
   Removing these settings does not undo the recovery. While `AdminPassword` stays set,
   every configuration reload restores that password and removes two-factor enrolment.
4. **Optional fallback: Debug sign-in.** If you need this alternative to the recovery steps
   above, read the warning and restrict backoffice access to trusted operators first.
   In `appsettings.json`, explicitly set **both** `BreakGlass:DebugSignIn` and
   `Umbraco:CMS:Hosting:Debug` to `true`, then save. Sign in with an existing enabled
   account's username and any nonempty password. As soon as you have access, set
   `BreakGlass:DebugSignIn` back to `false` or remove it, turn debug mode off if you enabled
   it for recovery, and save before repairing the account. Turning on debug mode alone
   does not enable this fallback.

### Environment variables (startup only)

BreakGlass also supports environment variables through the site's standard .NET
configuration. Set them **before Umbraco starts**, using `__` in place of `:`, for example
`BreakGlass__AdminPassword`, `BreakGlass__AdminUserName` or `BreakGlass__AdminEmail`.
Environment variables are loaded at startup; changes or removals require an Umbraco restart.

With the default configuration, environment variables override matching values in
`appsettings.json`. For startup recovery, remove the recovery variables and any matching
file settings after regaining access, then restart Umbraco **before** choosing your final
administrator password or re-enrolling in two-factor authentication.

## Security

- **Debug sign-in is off by default and works only in debug mode.** It applies while
  `Umbraco:CMS:Hosting:Debug` **and** `BreakGlass:DebugSignIn` are both true, and never outside
  debug mode, whatever `DebugSignIn` says. Both are read on every sign-in, so setting either
  to `false` in the configuration file stops further bypassed sign-ins when it reloads,
  without a restart. The environment name (`Development`, `Production`) plays no part.
- **While Debug sign-in applies, anyone who reaches the backoffice login can sign in as any
  enabled account whose username they know.** Restrict backoffice access to trusted operators
  before enabling it on an internet-accessible site. Unknown usernames, empty passwords and
  disabled accounts are still refused.
  Each sign-in unlocks a locked account, logs a warning and leaves the account's password
  and two-factor enrolment as they are.
- **The configuration is a credential.** Whoever can change the site's configuration can
  set the administrator's password. Protect the recovery settings as you would a password
  and remove them after use. No log message contains the password.
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
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=17.0.0
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release
dotnet pack src/AurorisNet.Umbraco.BreakGlass.csproj -c Release -o artifacts/packages
```

The package compiles against `UmbracoMinimumVersion` (17.0.0) and supports Umbraco
`[17.0.0, 19.0.0)` on .NET 10. The test site's `UmbracoVersion` defaults to 18.3.1; both
properties are in `Directory.Build.props`. Override `UmbracoVersion` to run the full suite
against another supported release without changing the package's compilation baseline.
A local package is numbered `0.0.0-local`. Render `docs/images/icon.png` from `icon.svg` at
128 by 128 pixels.

The [compatibility guide](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/compatibility.md)
describes the API wrapper boundary and records the full 25-test suite passing on Umbraco
17.0.0, 17.8.2, 18.0.0 and 18.3.1. A new CMS major requires an explicit adapter and tests.

To publish, run **Actions → Release → Run workflow** on `main` with a semantic version such
as `1.0.1`. The workflow builds with warnings as errors, runs the full tests against Umbraco
17.0.0 and the default 18.x test version, packs, tags `v<version>`, pushes to nuget.org
through trusted publishing as `AurorisNet` and creates the GitHub release. **dry-run** stops
after packing and keeps the package as an artifact for
seven days. A version with a suffix, such as `1.1.0-rc.1`, is published as a prerelease.

## License

[MIT](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/LICENSE)
