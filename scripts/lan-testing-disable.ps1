# Disable LAN testing mode and restore localhost-only configuration.
# Usage: Run as Administrator: .\scripts\lan-testing-disable.ps1
#
# After running this script, restart the API and frontend normally:
#   cd Happie.Api; func start
#   dotnet run --project Happie.Web --launch-profile http

#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

Write-Host "Disabling LAN testing mode..." -ForegroundColor Cyan

# 1. Remove firewall rules.
netsh advfirewall firewall delete rule name="Happie Dev Server" | Out-Null
netsh advfirewall firewall delete rule name="Happie Dev API" | Out-Null
Write-Host "  [OK] Firewall rules removed"

# 2. Restore launchSettings.json to localhost only.
$launchSettings = Get-Content "Happie.Web\Properties\launchSettings.json" -Raw
$launchSettings = $launchSettings -replace '"applicationUrl": "http://0\.0\.0\.0:5195"', '"applicationUrl": "http://localhost:5195"'
Set-Content "Happie.Web\Properties\launchSettings.json" $launchSettings
Write-Host "  [OK] launchSettings.json -> localhost:5195"

# 3. Restore appsettings.json to production URL (replace any LAN IP pattern).
$appSettings = Get-Content "Happie.Web\wwwroot\appsettings.json" -Raw
$appSettings = $appSettings -replace '"ApiBaseUrl": "http://[\d\.]+:7071/api/"', '"ApiBaseUrl": "https://happie-func.azurewebsites.net/api/"'
Set-Content "Happie.Web\wwwroot\appsettings.json" $appSettings
Write-Host "  [OK] appsettings.json -> production URL"

# 4. Restore appsettings.Development.json (replace any LAN IP pattern).
$appSettingsDev = Get-Content "Happie.Web\wwwroot\appsettings.Development.json" -Raw
$appSettingsDev = $appSettingsDev -replace '"ApiBaseUrl": "http://[\d\.]+:7071/api/"', '"ApiBaseUrl": "http://localhost:7071/api/"'
Set-Content "Happie.Web\wwwroot\appsettings.Development.json" $appSettingsDev
Write-Host "  [OK] appsettings.Development.json -> localhost:7071"

# 5. Restore CORS in local.settings.json (remove any LAN IP entry).
$localSettings = Get-Content "Happie.Api\local.settings.json" -Raw
$localSettings = $localSettings -replace '"CORS": "http://localhost:5195,http://[\d\.]+:5195"', '"CORS": "http://localhost:5195"'
Set-Content "Happie.Api\local.settings.json" $localSettings
Write-Host "  [OK] local.settings.json CORS -> localhost only"

Write-Host ""
Write-Host "LAN testing mode disabled. Back to localhost-only." -ForegroundColor Cyan
Write-Host "Restart the API and frontend to apply changes."
