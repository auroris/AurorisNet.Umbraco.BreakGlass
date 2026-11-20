# Umbraco Break Glass

`AurorisNet.Umbraco.BreakGlass` lets whoever controls an Umbraco 17 or 18 site's
configuration get back into its backoffice when the administrator is locked out.

![MIT license](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/license-mit.svg)
![.NET 10](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-10.svg)
![Umbraco 17 and 18](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/umbraco-17-18.svg)

The package gives two ways in:

- **A configured administrator password.** Set `BreakGlass:AdminPassword`, and the package
  sets the built-in administrator's password, unlocks the account, clears its failed sign-in
  attempts and removes its two-factor enrolment. `BreakGlass:AdminUserName` and
  `BreakGlass:AdminEmail` change the account's username and email address. The settings
  apply when the site starts and whenever its configuration reloads.
- **Debug sign-in.** While `BreakGlass:DebugSignIn` and `Umbraco:CMS:Hosting:Debug` are both
  `true`, any nonempty password signs in to an existing, enabled account. It is off by
  default. Read the warning under **Settings** before you turn it on.

## Install

Add the package to a website project that runs Umbraco 17 or 18:

```xml
<PackageReference Include="AurorisNet.Umbraco.BreakGlass" Version="1.*" />
```

The package registers itself through an Umbraco composer, so the site needs no code. After
the site restarts, it reads the `BreakGlass` settings.

## Settings

The settings are in the `BreakGlass` configuration section. The three `Admin` settings apply
to the built-in administrator (user ID `-1`), whatever its current username.

| Setting | Default | What it does |
|---|---|---|
| `BreakGlass:AdminPassword` | empty | Sets the administrator's password exactly as written, spaces included, through Umbraco's password rules. Then unlocks the account, clears its failed sign-in attempts and removes its two-factor enrolment. Empty, or spaces only, does none of this. |
| `BreakGlass:AdminUserName` | empty | Sets the administrator's username. Empty keeps the current username. Must be an email address while `Umbraco:CMS:Security:UsernameIsEmail` is `true`. |
| `BreakGlass:AdminEmail` | empty | Sets the administrator's email address. Empty keeps the current address. Must be an email address. |
| `BreakGlass:DebugSignIn` | `false` | While this setting and `Umbraco:CMS:Hosting:Debug` are both `true`, any nonempty password signs in to an existing, enabled account, past lockout and two-factor checks. Both settings are read on every sign-in. |

> **Warning: `DebugSignIn` bypasses authentication for existing backoffice accounts,
> administrators included.** While `BreakGlass:DebugSignIn` and `Umbraco:CMS:Hosting:Debug`
> are both `true`, anyone who can reach the backoffice login and knows an enabled account's
> username can sign in as that account with any nonempty password. They need neither the
> account's password nor a two-factor code, and the sign-in lifts an account lockout. Unknown
> usernames, empty passwords and disabled accounts are still refused.

### When the administrator settings apply

The package applies the three `Admin` settings after Umbraco has started, and again each
time the configuration reloads, so saving `appsettings.json` changes a running site. It
applies them only while Umbraco's runtime level is `Run`. If the site starts at another
level, for example because it waits for an upgrade, restart it once Umbraco can run.

Each time, the package works in this order:

1. It sets the username and email address when they differ from the account's. If Umbraco
   rejects them, the package logs the error and stops, with the password unchanged.
2. It sets the password when it differs from the account's. If Umbraco's password rules
   reject it, the package logs the error and stops. The existing password, a lockout and the
   two-factor enrolment all stay.
3. It unlocks the account, clears its failed sign-in attempts and removes its two-factor
   enrolment. This step also runs when the configured password is already the account's.

Without `AdminPassword`, the package stops after step 1. While `AdminPassword` stays set,
every start and every configuration reload restores that password and removes two-factor
enrolment.

### Settings that stop the site

The site stops at startup with a configuration error when:

- `DebugSignIn` is something other than `true` or `false`;
- `AdminEmail` is not an email address;
- `AdminUserName` is not an email address while `UsernameIsEmail` is `true`.

When a running site reloads settings with one of these errors, the package logs the error,
leaves the administrator as it is and turns Debug sign-in off until the settings are
corrected. Normal sign-in continues, and changes the package applied earlier stay.

## Recover a locked-out account

1. **Set a new password.** Add it to `appsettings.json` and save the file:

   ```json
   "BreakGlass": {
     "AdminPassword": "A new password that meets the site's rules"
   }
   ```

   Add `AdminUserName` and `AdminEmail` to the same section if they need changing too. A
   running site applies the settings when the file reloads, and a stopped site when Umbraco
   starts.
2. **Sign in as the administrator** with the configured password and the account's username,
   which is the new username if you changed it.
3. **Remove the recovery settings** from the `BreakGlass` section and save the file. The
   recovered account keeps its new password. Do this before you choose your final password
   and enrol in two-factor authentication again: while `AdminPassword` stays set, each
   configuration reload restores it and removes the enrolment.

**With environment variables.** The settings can also come from environment variables, with
`__` in place of `:`, such as `BreakGlass__AdminPassword`. .NET reads them when the site
starts, and by default they override the same settings in `appsettings.json`. After you
regain access, remove the variables and any matching settings in the file, and restart the
site before you choose your final password or enrol in two-factor authentication again.

**With Debug sign-in.** Read the warning under **Settings** and restrict access to the
backoffice to trusted operators first. Set both `BreakGlass:DebugSignIn` and
`Umbraco:CMS:Hosting:Debug` to `true` in `appsettings.json` and save the file. Sign in with
an existing, enabled account's username and any nonempty password. As soon as you are in,
set `DebugSignIn` back to `false` or remove it, turn debug mode off if you turned it on for
the recovery, and save the file before you repair the account.

## Security

- **Debug sign-in works only in debug mode.** It applies while `Umbraco:CMS:Hosting:Debug`
  and `BreakGlass:DebugSignIn` are both `true`. Both are read on every sign-in, so setting
  either to `false` in the configuration file stops it when the file reloads, without a
  restart. The environment name, such as `Development` or `Production`, plays no part.
- **While Debug sign-in applies, anyone who reaches the backoffice login can sign in as any
  enabled account whose username they know.** Restrict access to the backoffice to trusted
  operators before you turn it on. Each Debug sign-in unlocks a locked account, records the
  sign-in time and logs a warning with the user's ID. It leaves the account's password and
  two-factor enrolment as they are.
- **The configuration is a credential.** Whoever can change the site's configuration can set
  the administrator's password. Protect the recovery settings as you would a password, and
  remove them after use. The package's log messages leave the password out.
- **The package replaces Umbraco's backoffice sign-in manager.** Its own sign-in manager
  derives from Umbraco's and takes the place of every registration, including one the site
  registered itself. Outside Debug sign-in it runs Umbraco's sign-in unchanged.
- **The package puts its password checker in front of the site's
  `IBackOfficeUserPasswordChecker`.** While Debug sign-in applies, the checker accepts any
  nonempty password. Otherwise it passes each check to the site's checker, or to Umbraco's
  own password check when the site has none.

Report a vulnerability as the
[security policy](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/SECURITY.md)
describes.

## Supported versions

| Umbraco | Supported releases | Runtime |
|---|---|---|
| 18 | <!-- umbraco-releases 18 -->18.0.0 to 18.3.2<!-- /umbraco-releases --> | .NET 10 |
| 17 | <!-- umbraco-releases 17 -->17.0.0 to 17.8.2<!-- /umbraco-releases --> | .NET 10 |

The package accepts every release in these ranges, and the tests run on the first and the
newest release of each major version. One package supports both major versions, with the
Umbraco dependency range `[17.0.0, 19.0.0)`.

## Documentation

| Guide | Covers |
|---|---|
| [Umbraco compatibility](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/compatibility.md) | How one build supports both Umbraco major versions, the types in `src/Compatibility`, and what the tests check. |
| [Security policy](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/SECURITY.md) | How to report a vulnerability, and which access the package gives by design. |

## Developing

Building needs the .NET 10 SDK, which `global.json` selects, and PowerShell 7 for the
scripts.

```powershell
dotnet build -c Release
# The tests, on the newest supported Umbraco release
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release
# The tests on another release
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=17.0.0
# Add an Umbraco release by hand, with AurorisNet.Build cloned beside this repository
pwsh ../AurorisNet.Build/scripts/Add-UmbracoRelease.ps1 -Releases 18.3.2
# A local package, numbered 0.0.0-local
dotnet pack src/AurorisNet.Umbraco.BreakGlass.csproj -c Release -o artifacts/packages
```

The tests start Umbraco sites on SQLite in a temporary folder, sign in through the
backoffice login endpoint and change the settings by saving `appsettings.json`.

The full matrix is the test command once for each release in `UmbracoReleases` in
`Directory.Build.props`. It runs on a developer's machine. GitHub tests the newest supported
release, and each new Umbraco release when it appears.

`Add-UmbracoRelease.ps1` adds the release to `UmbracoReleases`, moves `UmbracoVersion` to the
newest listed release, and rewrites the ranges in the **Supported versions** table.

### Add an Umbraco major version

`Add-UmbracoRelease.ps1` and the weekly workflow accept releases of the listed major versions
only. When Umbraco publishes a new major version, the workflow opens an issue titled
`Support Umbraco <major>`, and a person adds it:

1. If it runs on a newer .NET, raise the SDK in `global.json`, add the SDK to the workflows'
   `setup-dotnet` steps, and add the target framework to `Directory.Build.props`.
2. Add an adapter such as `Umbraco19Api` in `src/Compatibility`, with any Umbraco call that
   differs from `UmbracoApi`, and add it to `UmbracoCompatibility.ImplementationType`.
3. Raise the upper bound of the Umbraco dependency range in `Directory.Packages.props`, and
   the unsupported major versions in `CompatibilityTests`.
4. Add its first release to `UmbracoReleases` in `Directory.Build.props`, and set
   `UmbracoVersion` to it.
5. Add a row to the **Supported versions** table with `umbraco-releases 19` markers like
   those in the rows above, and add the major version to the text and badge that name the
   supported versions.
6. Run the tests on that release, and on the first and the newest release of the other major
   versions.

## Releases

The package follows Umbraco. Each week `umbraco-releases.yml` looks on nuget.org for new
Umbraco releases and tests the package on each. A release that passes is added to
`UmbracoReleases` and to the **Supported versions** table, and `weekly-release.yml` then starts
`release.yml`, which publishes the package to nuget.org and GitHub. A release that fails gets an issue.

Versions are `MAJOR.MINOR.PATCH`. The package is not on nuget.org yet: start the first
release by hand, by running `release.yml` with a version such as `1.0.0`.

The [release guide](https://github.com/auroris/AurorisNet.Build/blob/main/docs/releasing.md)
describes the workflows and how to start a release by hand.

## License

[MIT](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/LICENSE)
