# Umbraco Break Glass

Emergency backoffice access for Umbraco 18 sites. Whoever can edit the site's
`appsettings.json` gets back into the backoffice, without the database, a password reset
email or a restart.

## What it does

- **A configured administrator password.** Set `BreakGlass:AdminPassword` and save the
  file. The built-in administrator's password changes at once, and the account is unlocked,
  its failed attempts are cleared and its two-factor enrolment is removed. The username and
  email can be set the same way. Empty, missing or rejected values leave the account alone.
- **Debug sign-in.** While `Umbraco:CMS:Hosting:Debug` is true, the backoffice accepts any
  nonempty password for an existing account and skips lockout and two-factor checks.
  `BreakGlass:DebugSignIn` turns it off. Outside debug mode it never applies.

The package registers itself through Umbraco's composers, and installing it is the whole
setup.

## Quick start

```xml
<PackageReference Include="AurorisNet.Umbraco.BreakGlass" Version="1.*" />
```

To recover the administrator, add a password to `appsettings.json` and save it:

```json
"BreakGlass": {
  "AdminPassword": "A new password that meets the site's rules"
}
```

Sign in as the administrator with that password, then remove the setting once the account
is in order again.

## Settings

- `BreakGlass:DebugSignIn` (default `true`): accepts any nonempty password for an existing
  account while `Umbraco:CMS:Hosting:Debug` is true.
- `BreakGlass:AdminPassword`: the built-in administrator's password; applying it also
  unlocks the account and removes its two-factor enrolment.
- `BreakGlass:AdminUserName` (default `admin`): the administrator's username. Empty leaves
  it alone. While `Umbraco:CMS:Security:UsernameIsEmail` is true it must be an email
  address, and leaving it out keeps the current one.
- `BreakGlass:AdminEmail`: the administrator's email address. Empty leaves it alone.

Saving the file applies every setting to a running site.

## Documentation

- [Behavior and security](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/BEHAVIOR.md)
- [Issue tracker](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/issues)

Licensed under the [MIT license](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/LICENSE).
