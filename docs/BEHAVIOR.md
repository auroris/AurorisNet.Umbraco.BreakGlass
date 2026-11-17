# Behavior and security

The package adds two ways back into a backoffice: a configured administrator password and
debug sign-in. It registers through `.AddComposers()`. The composer binds the `BreakGlass`
settings, adds the service that applies the administrator's credentials, and adds a
collection builder that replaces the sign-in services when `IUmbracoBuilder.Build()`
finalizes dependency injection, after every composer has registered its own.

## Configured administrator password

The built-in administrator is the account with ID `-1` (`Constants.Security.SuperUserKey`).
It stays the target when its username or email changes, so the setting keeps working on a
database whose administrator was renamed.

| When | What happens |
|---|---|
| Startup | After Umbraco's `UmbracoApplicationStartedNotification`, which follows an unattended install or upgrade, the settings are applied once. |
| The configuration reloads | Each reload, such as a saved `appsettings.json`, queues one update. Several notifications for one save collapse into one, and each update reads the configuration as it is then. |

Each update does the following, in order:

1. **Username and email.** `BreakGlass:AdminUserName` and `BreakGlass:AdminEmail` are
   trimmed and applied when they differ from the account's. Empty values leave the field
   alone. Left out, the username is `admin` where usernames are not email addresses, and
   stays as it is where they are, because Umbraco's login form then asks for an email.
2. **Password.** `BreakGlass:AdminPassword` is used exactly as written, spaces included,
   and goes through Umbraco's password rules. The stored hash is compared first, so an
   unchanged password leaves the hash and the security stamp alone: saving the file for an
   unrelated reason does not sign the administrator out elsewhere.
3. **Recovery.** A valid configured password, changed or not, lifts the account's lockout,
   clears its failed attempts and removes every two-factor enrolment, since a lost
   authenticator is the other way an administrator is locked out.

An empty, missing or whitespace password skips steps 2 and 3. A password Umbraco rejects
is logged with Umbraco's reasons and leaves the password, lockout and enrolment as they
were; correcting the setting and saving again retries. No log message contains a password.

Remove the password from the settings once the account is in order. While it is set, every
save of the file applies it again, and two-factor enrolment cannot be kept on the
administrator.

## Debug sign-in

While debug sign-in applies, the backoffice's password sign-in accepts any nonempty
password for an existing, approved account:

- a locked account is unlocked and its failed attempts cleared, because Umbraco's
  authorize step signs a locked account out again;
- two-factor verification is skipped;
- the successful login is recorded, and the account's password, security stamp and
  two-factor enrolment stay as they are, so repairs remain explicit actions in **Users**;
- each such sign-in is logged as a warning.

Unknown usernames, empty passwords and disabled accounts are refused as usual. The
password checker (`IBackOfficeUserPasswordChecker`) also accepts any nonempty password at
those times, so code that calls `CheckPasswordAsync` agrees with the sign-in. A checker
the site had registered stays behind it and decides whenever debug sign-in does not apply.

The package replaces Umbraco's backoffice sign-in manager with a subclass of
`BackOfficeSignInManager` through the four services Umbraco resolves it by:
`SignInManager<BackOfficeIdentityUser>`, `BackOfficeSignInManager`,
`IBackOfficeSignInManager` and `ICoreBackOfficeSignInManager`. A site that registers its
own sign-in manager will have it replaced. When debug sign-in does not apply, every call
goes to Umbraco's own implementation unchanged.

## Security

- **Debug sign-in never works outside debug mode.** It applies only while
  `Umbraco:CMS:Hosting:Debug` is true **and** `BreakGlass:DebugSignIn` is true. Both are
  read for every sign-in, so switching debug mode off in a running site closes it at once.
  The environment name (`Development`, `Production`) plays no part.
- **Settings that fail to load close it.** When the `BreakGlass` settings cannot be read,
  for example after saving `"DebugSignIn": "maybe"`, debug sign-in is off and the error is
  logged until the settings are corrected.
- **Leave debug mode off in production.** Anyone who can reach the backoffice login while
  it is on can sign in as any account whose username they know. Use it for development,
  or for a deliberate recovery: switch debug mode on, sign in, repair the account in
  **Users**, and switch it off again. Set `BreakGlass:DebugSignIn` to `false` on a site
  that must keep debug mode on for other reasons.
- **The settings file is a credential.** Whoever can write `appsettings.json`, or set the
  matching environment variables, can take over the administrator account. That is the
  point of the package, and it is also why the file's permissions matter. Prefer an
  environment variable or a secret store for `BreakGlass:AdminPassword` on shared servers,
  and remove it after use.

## Startup validation

A site does not start when:

- `BreakGlass:DebugSignIn` is not `true` or `false`;
- `BreakGlass:AdminEmail` is set but is not an email address;
- `BreakGlass:AdminUserName` is set but is not an email address while
  `Umbraco:CMS:Security:UsernameIsEmail` is true, because the administrator could not type
  it into the login form.

The password is not validated at startup: Umbraco's password rules decide when it is
applied, and a rejected password is logged without stopping the site. When a running site
reloads settings that fail these checks, the administrator's credentials are left alone
and the error is logged.

## Testing

[Developing](DEVELOPING.md) shows how to run the tests.

| Test | Checks |
|---|---|
| Settings | Defaults, trimming, empty values, the debug mode requirement, the password checker with and without a site's own checker, invalid settings closing debug sign-in, and startup validation. |
| Configured password | On a site composed through ordinary composer discovery: the password applies at startup; a saved file recovers a locked account with two-factor enrolment; the password keeps its spaces; a rejected or empty password leaves the account alone; a saved username and email apply; no password reaches the log. |
| Debug sign-in | On a running site: any nonempty password signs in a locked account with two-factor enrolment, without changing its credentials; empty passwords and unknown users are refused; saving `DebugSignIn: false`, or switching debug mode off, restores normal sign-in with the real password and two-factor checks; switching debug mode back on restores debug sign-in. |
