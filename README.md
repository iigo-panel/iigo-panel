# IIGO Panel

Self-hosted ASP.NET application using Blazor. Designed to work on Windows Server 2012 and newer.

Windows Server 2012 may require installing Microsoft Visual C++ 2015-2022 Redistributable:

* [VC 2015 x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
* [VC 2015 x86](https://aka.ms/vs/17/release/vc_redist.x86.exe)

[Join Discord](https://discord.gg/cKqN9wa2d9)

## Installing

Run `IIGO Panel.msi` as an administrator. It installs the panel to `C:\Program Files\IIGO Panel` (you can pick another folder), registers the `IIGO` event log, and installs and starts the `IIGOPanel` Windows service. The service runs as LocalSystem, starts with Windows, and restarts itself if it stops unexpectedly.

To upgrade, run the newer `IIGO Panel.msi`. It stops the service, replaces the program files in the same folder, and starts the service again. These are kept:

* The `Data` folder (database and encryption keys).
* `appsettings.json`, including your HTTPS endpoint. A new version never overwrites it, so add any new settings by hand.

Uninstalling removes the service and program files but leaves `Data` and `appsettings.json` behind, so a reinstall picks up where you left off. Delete the folder yourself to remove everything.

The installer limits the `Data` folder to SYSTEM and Administrators. Its keys are protected for the machine, so any account that can read them could decrypt stored secrets.

If you set the panel up as a service by hand before, remove that service first (`sc.exe delete "<service name>"`), and point the installer at the same folder to keep your data.

To build the installer, run this from a Visual Studio Developer prompt (the panel's COM reference needs Visual Studio's MSBuild). The package is written to `IIGO.Installer\bin\Release\IIGO Panel.msi`:

```
msbuild IIGO.Installer\IIGO.Installer.wixproj -restore /p:Configuration=Release
```

The MSI version comes from `IIGO.exe`'s file version. Pre-release builds all share `1.0.0.0`, so the installer lets a package replace an install of the same version.

## Getting Set Up

Modify `appsettings.json` as necessary if default settings need to be changed.

Visual Studio must run as Administrator for full testing/development (some features will work as limited user, but websites, app pools, system updates in particular have to be admin).

When the database has no users yet, an administrator is created with the following credentials. The password must be changed at the first sign-in; nothing else in the panel can be used until it is. The account is only created on an empty database, so deleting it later is permanent. Installs upgraded from earlier versions that still use a default password are also required to change it.

```
Username: admin
Password: IIGOAdmin#10
```

## HTTPS

The panel is designed to be reachable from the Internet, so the sign-in cookie is only sent over HTTPS. Sign-in will not work over plain HTTP, and the service logs an error at startup if no HTTPS endpoint is configured. Add a Kestrel endpoint to `appsettings.json` with a certificate from the local machine store (or a `.pfx` file), for example on port 8443:

```json
"Kestrel": {
  "Endpoints": {
    "Https": {
      "Url": "https://*:8443",
      "Certificate": {
        "Subject": "panel.example.com",
        "Store": "My",
        "Location": "LocalMachine"
      }
    }
  }
}
```

Alternatively, run the panel behind a reverse proxy that terminates TLS and forwards the client address.

Sign-in is limited to 10 attempts per minute per address, and an account is locked for 15 minutes after 5 failed attempts. Passwords must be at least 12 characters.

Set `AllowedHosts` in `appsettings.json` to the host name the panel is reached on (for example `"AllowedHosts": "panel.example.com"`) so requests with a forged `Host` header are rejected.

## Settings

The Settings page (Administration > Settings) holds sign-in security and the settings for each notification service. Users whose role has the `View Settings` feature can see it; only administrators can change it.

Two-factor authentication can be required for administrators or for everyone. Users it applies to are sent to set up an authenticator app before they can use the panel, and can't turn 2FA off. API keys are not affected.

Email goes through one service, picked from the Email card's dropdown (SMTP, SendGrid, Postmark or Amazon SES). Monitors that send email use whichever service is picked there. Each notification service has a "Send test message" button that uses its saved settings and shows the error if sending fails. Choose which services each monitor sends through on the monitor's own page.

## Secrets and keys

Notification secrets (the SMTP password, SendGrid and Postmark keys, AWS secret keys, and Discord, Slack and Google Chat webhook URLs) are encrypted before they're stored in the database. A value typed straight into the `ConfigSetting` table as plain text is encrypted the first time it's used. The encryption keys, which also protect sign-in cookies and password reset tokens, are kept in the `Data` folder next to the database, protected with DPAPI for the machine. Back that folder up with the database: without the keys, stored secrets have to be entered again and everyone has to sign in again.

## Permissions

Pages are only available to users whose role has the matching feature, except the Administrator role, which has everything. The System Status and Processes pages need the `View System Status` feature, because the process list shows every process's command line. Roles and their permissions, users and API keys can only be changed by administrators.

## API

Create a key on the API Keys page and send it in the `X-API-KEY` header. A key can do everything the API offers, so treat it like an administrator password. Each address can make 60 calls a minute. Errors come back as JSON problem details with a `detail` or `errors` message. Times are UTC, and enums are names such as `Started` or `Inbound`.

| Area | Endpoints |
| --- | --- |
| Websites | `GET /api/websites`, `GET /api/websites/{id}`, `POST /api/websites`, `DELETE /api/websites/{id}`, `POST /api/websites/{id}/start` · `stop` · `restart`, `PUT /api/websites/{id}/path` |
| Bindings | `POST` · `PUT /api/websites/{id}/bindings`, `DELETE /api/websites/{id}/bindings?protocol=&bindingInformation=`, `GET /api/websites/certificates` |
| Applications | `POST /api/websites/{id}/applications`, `PUT` · `DELETE /api/websites/{id}/applications?path=/alias` |
| App pools | `GET /api/apppools`, `GET /api/apppools/{name}`, `GET /api/apppools/{name}/applications`, `POST /api/apppools`, `DELETE /api/apppools/{name}`, `POST /api/apppools/{name}/start` · `stop` · `recycle` |
| Firewall | `GET /api/firewall/rules?search=`, `POST /api/firewall/rules`, `PUT` · `DELETE /api/firewall/rules?name=&direction=` |
| Scheduled tasks | `GET /api/tasks?search=`, `GET /api/tasks/{name}`, `POST /api/tasks`, `PUT` · `DELETE /api/tasks/{name}`, `POST /api/tasks/{name}/run` · `stop` · `enable` · `disable` |
| Windows updates | `GET /api/updates`, `POST /api/updates/check` (starts a search; installing is only on the Check Updates page) |
| Services | `GET /api/services?search=`, `GET /api/services/{name}`, `POST /api/services/{name}/start` · `restart` (stopping, adding, changing and deleting are only on the Services page) |
| App Pool monitor | `GET /api/monitors/apppool`, `GET /api/monitors/apppool/events?days=30`, `GET` · `PUT /api/monitors/apppool/settings` |
| HTTPS monitor | `GET /api/monitors/https`, `GET /api/monitors/https/events?days=30`, `GET` · `PUT /api/monitors/https/settings` |
| IP restrictions | `GET` · `POST` · `DELETE /api/IPSecurityRules` |

Notes:

- `POST /api/websites` creates a site with one http binding. Add https bindings afterwards; they need `certificateThumbprint` (the store is found if you leave `certificateStore` out).
- Firewall rules have no ID. A rule is picked by `name`, plus `direction` when several rules share a name. A name that still matches more than one rule returns 409.
- Scheduled tasks are the ones in Task Scheduler's root folder, picked by name. `PUT` replaces the whole task, so send back what `GET` returned with your changes; a new `name` renames it. Tasks that run as a user with a stored password need `password` on every save. Trigger `start` and `expires` are the server's local time, since schedules follow its clock; a time sent with `Z` or an offset is converted. Tasks with `"editable": false` can only be run, stopped, enabled, disabled or deleted.
- `PUT .../settings` changes only the settings you send, as `{"settings": {"Name": "value"}, "notificationServices": ["Email"]}`. Leave out `notificationServices` to keep them. `GET .../settings` lists the setting names and the notification services you can use.
- `GET /api/IISApi/sites` still works for existing callers. It returns the same list as `GET /api/websites`.
