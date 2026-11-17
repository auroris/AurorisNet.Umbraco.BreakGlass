<h1 align="center">Umbraco Break Glass</h1>

<p align="center">
  Get back into an Umbraco backoffice when the administrator is locked out, from the site's own settings file.
</p>

<p align="center">
  <a href="LICENSE"><img alt="MIT license" src="docs/images/badge-license.svg"></a>
  <img alt=".NET 10" src="docs/images/badge-dotnet-10.svg">
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/AurorisNet.Umbraco.BreakGlass">NuGet package</a> ·
  <a href="#settings">Settings</a> ·
  <a href="docs/BEHAVIOR.md#security">Security</a>
</p>

## What it does

`AurorisNet.Umbraco.BreakGlass` gives whoever can edit a site's `appsettings.json` a way
back into its backoffice, without the database, a password reset email or a restart. It
registers itself through Umbraco's composers, and installing it is the whole setup.

- **A configured administrator password.** Set `BreakGlass:AdminPassword` and save the
  file. The built-in administrator's password changes at once, and the account is unlocked,
  its failed attempts are cleared and its two-factor enrolment is removed, so nothing stands
  between that password and the account. The username and email can be set the same way.
  Empty, missing or rejected values leave the account alone.
- **Debug sign-in.** While `Umbraco:CMS:Hosting:Debug` is true, the backoffice accepts any
  nonempty password for an existing account and skips lockout and two-factor checks. A
  developer signs in to a copy of a site without knowing its passwords, and a deployed site
  can be opened in an emergency by switching debug mode on. `BreakGlass:DebugSignIn` turns
  it off. Outside debug mode it never applies, whatever the settings say.

[Behavior and security](docs/BEHAVIOR.md) describes exactly what each feature changes and
what it leaves alone.

## Quick start

Add the package to a website running Umbraco 18:

```xml
<PackageReference Include="AurorisNet.Umbraco.BreakGlass" Version="1.*" />
```

To recover the administrator, add a password to `appsettings.json` and save it:

```json
"BreakGlass": {
  "AdminPassword": "A new password that meets the site's rules"
}
```

Sign in as the administrator with that password, then remove the setting once the account is
in order again: while it is set, every save of the file applies it again, and two-factor
enrolment cannot be kept on the administrator.

## Settings

| Setting | Default | Effect |
|---|---|---|
| `BreakGlass:DebugSignIn` | `true` | Accepts any nonempty password for an existing account while `Umbraco:CMS:Hosting:Debug` is true. Has no effect outside debug mode. |
| `BreakGlass:AdminPassword` | empty | The built-in administrator's password. Applying it also unlocks the account and removes its two-factor enrolment. |
| `BreakGlass:AdminUserName` | `admin` | The built-in administrator's username. Empty leaves it alone. While `Umbraco:CMS:Security:UsernameIsEmail` is true, it must be an email address, and leaving it out keeps the current one. |
| `BreakGlass:AdminEmail` | empty | The built-in administrator's email address. Empty leaves it alone. |

Saving the file applies every setting to a running site. A site that starts with an
invalid email address, or with a username the login form could not accept, stops with a
configuration error instead of quietly failing to recover the account.

## Documentation

| Guide | Read it for |
|---|---|
| [Behavior and security](docs/BEHAVIOR.md) | What the package changes in Umbraco's sign-in, the security rules it keeps, and test coverage. |
| [Developing](docs/DEVELOPING.md) | Building, testing and publishing a release. |
| [Security policy](SECURITY.md) | Reporting a vulnerability. |

## License

[MIT](LICENSE)
