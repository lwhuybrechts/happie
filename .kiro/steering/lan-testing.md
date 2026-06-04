---
inclusion: manual
---
# LAN Testing — iPhone Access to Localhost

## Overview

The app can be tested on a physical iPhone by exposing the local dev servers on the LAN. Two PowerShell scripts automate the config changes.

## Prerequisites

- iPhone must be on the **same Wi-Fi network** as the dev machine.
- Scripts must be run **as Administrator** (they manage firewall rules).

## Enable LAN Testing

```powershell
.\scripts\lan-testing-enable.ps1
```

The script auto-detects your LAN IP address. It prints the URL to visit on your iPhone at the end.

Then restart the servers:
- API: `func start --host 0.0.0.0` (from `Happie.Api/`)
- Frontend: `dotnet run --project Happie.Web --launch-profile http`

On iPhone, visit: `http://<LAN_IP>:5195`

## Disable LAN Testing

```powershell
.\scripts\lan-testing-disable.ps1
```

Then restart the servers normally (without `--host 0.0.0.0`).

## Important Notes

- The iPhone must be on the **same Wi-Fi network** as the dev machine.
- The API must be started with `--host 0.0.0.0` so it listens on all interfaces.
- iOS Safari does not support service workers over plain HTTP (non-localhost), so push notifications won't work during LAN testing. The service worker registration is guarded against this.
- The `appsettings.json` (production config) is temporarily changed during LAN mode because Blazor WASM falls back to "Production" environment when served from a LAN IP (no `blazor-environment` header match). **Do not commit** while LAN mode is enabled.
- Find your LAN IP with `ipconfig` (look for the Wi-Fi adapter's IPv4 address). The scripts auto-detect this.

## Cleanup

Run the disable script — it removes the firewall rules and reverts all config files:

```powershell
.\scripts\lan-testing-disable.ps1
```
