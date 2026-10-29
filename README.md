# IIGO Panel

Self-hosted ASP.NET application using Blazor. Designed to work on Windows Server 2012 and newer.

Windows Server 2012 may require installing Microsoft Visual C++ 2015-2022 Redistributable:

* [VC 2015 x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
* [VC 2015 x86](https://aka.ms/vs/17/release/vc_redist.x86.exe)

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

Sign-in is limited to 10 attempts per minute per address, and an account is locked for 15 minutes after 5 failed attempts.
