# Developing

This page covers building the package, running its tests and publishing a release. The
source is in `src`: the composer, the settings and their startup validation, the service
that applies the administrator's credentials, and debug sign-in, which is a gate read for
every sign-in, a password checker and a sign-in manager. The test project holds the
settings tests, the live-site tests and the fixture that hosts a site.

## Requirements

- The .NET 10 SDK. [`global.json`](../global.json) keeps every build on a .NET 10 SDK, also
  on a machine that has a newer one, and selects the Microsoft Testing Platform runner.
- PowerShell 7 to run the release workflow's steps by hand.

The tests need nothing else: each site uses its own SQLite database in a temporary folder.

## Build

```powershell
dotnet build -c Release
```

The package compiles against the oldest supported Umbraco release, `UmbracoMinimumVersion`
in [`Directory.Build.props`](../Directory.Build.props), and its NuGet dependency allows
any Umbraco 18 release from there. The tests build against `UmbracoVersion`, the newest
release they have been run on. Add `-p:UmbracoVersion=18.0.0` to build the tests against
another release.

Both projects generate XML documentation, so the compiler checks every doc comment. Keep
the build free of warnings: the Release workflow builds with
`ContinuousIntegrationBuild`, which turns a warning into an error. NuGet's vulnerability
reports stay warnings there.

To build a package:

```powershell
dotnet pack src/AurorisNet.Umbraco.BreakGlass.csproj -c Release -o artifacts/packages
```

A local package is numbered `0.0.0-local`, so NuGet's cache keeps it apart from published
versions. Add `artifacts/packages` as a NuGet source to try it in a separate site. The
package contains the assembly `BreakGlass.dll` with its embedded symbols and XML
documentation, the NuGet README, the icon and the MIT licence.

## Tests

```powershell
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release
```

The test project has two classes:

- **`SettingsTests`** checks the settings without a site: their defaults, trimming and
  empty values, the debug mode requirement, the password checker with and without a site's
  own checker behind it, invalid settings closing debug sign-in, and startup validation.
- **`SiteTests`** starts real sites with SQLite and Kestrel through ordinary composer
  discovery, and signs in through the backoffice's own login endpoint. One site has a
  configured password: it applies at startup, and saving `appsettings.json` recovers a
  locked account with two-factor enrolment, ignores empty and rejected passwords, and
  renames the administrator. The other site starts in debug mode: debug sign-in opens a
  locked account, and saving the settings switches it off, back to normal sign-in, and on
  again, through both `BreakGlass:DebugSignIn` and `Umbraco:CMS:Hosting:Debug`.

The fixture in `TestSite.cs` reads `Hosting:Debug` and the `BreakGlass` section only from
the content root's `appsettings.json`, which the tests rewrite while the site runs, so the
real file watcher delivers every change. The site tests run one at a time, because Umbraco
keeps static state; they take about a minute together.

To check the oldest supported release:

```powershell
dotnet build -c Release -p:UmbracoVersion=18.0.0
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release --no-build
dotnet build -c Release
```

The last build returns the tests to the newest release.

## Support a new Umbraco release

A minor Umbraco 18 release needs nothing unless it changes the extension points the package
relies on: the `BackOfficeSignInManager` constructor and its `PasswordSignInAsync`, the
four services that resolve the backoffice sign-in manager, `IBackOfficeUserPasswordChecker`
and `UmbracoApplicationStartedNotification`. Set `UmbracoVersion` to the new release, run
the tests, and publish a release if anything had to change.

A new major version starts by checking its target framework and those extension points,
then raising the dependency range in [`Directory.Packages.props`](../Directory.Packages.props)
and `UmbracoMinimumVersion`. A package that supports a new major version gets a new major
version of its own, and the README and `docs/NUGET.md` name the supported versions.

## Publish a release

Releases use semantic versions: a fix raises the patch number, a new setting or behaviour
the minor number, and a change that a site must act on, such as a renamed setting or a new
Umbraco major version, the major number.

Run **Actions → Release → Run workflow** on `main` with the version, such as `1.0.1`. The
workflow:

1. checks the version and rejects a tag that already exists on another commit;
2. runs the tests and packs the package with that version;
3. reserves the tag `v<version>`, so no other run can take the version;
4. signs in to NuGet with trusted publishing as `AurorisNet` and pushes the package;
5. creates the GitHub release with the package attached, generated notes and the notes
   from the run.

`dry-run` stops after step 2 and keeps the package as an artifact for seven days. A failed
run can be started again with the same version: a tag on the same commit is reused, NuGet
skips a package it already has, and an existing GitHub release receives the package again.
A version with a prerelease suffix, such as `1.1.0-rc.1`, is published as a prerelease.

Trusted publishing needs a policy on nuget.org for the `AurorisNet` account that names this
repository and `release.yml`.

The workflow pins each action to a commit, with its version in a comment beside it.
Dependabot opens a pull request when an action publishes a newer release. Pin the hash
of a commit: a version tag can have a hash of its own, which stops resolving once the
tag moves to a later release.

## Documents

`docs/NUGET.md` is the package's page on nuget.org, which shows Markdown without HTML or
relative links. Keep it in step with the README when settings change.
`docs/images/icon.svg` is the source of `icon.png`; render it at 128 by 128 pixels after
changing it, for example with the `sharp` Node package.
