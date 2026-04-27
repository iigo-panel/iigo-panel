# IIGO Panel

Self-hosted ASP.NET application using Blazor. Designed to work on Windows Server 2012 and newer.

Windows Server 2012 may require installing Microsoft Visual C++ 2015-2022 Redistributable:

* [VC 2015 x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
* [VC 2015 x86](https://aka.ms/vs/17/release/vc_redist.x86.exe)

## Getting Set Up

Modify `appsettings.json` as necessary if default settings need to be changed.

Visual Studio must run as Administrator for full testing/development (some features will work as limited user, but websites, app pools, system updates in particular have to be admin).

On first-run a new admin user is created with the following credentials (these can be adjusted in `Program.cs`:

```
Username: admin
Password: IIGOAdmin#10
```
