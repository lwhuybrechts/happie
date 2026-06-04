# Enable LAN testing mode for iPhone access.
# Usage: Run as Administrator: .\scripts\lan-testing-enable.ps1
#
# After running this script, restart the API and frontend:
#   cd Happie.Api; func start --host 0.0.0.0
#   dotnet run --project Happie.Web --launch-profile http
#
# Then on iPhone, visit the URL printed at the end of this script.

#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

# Auto-detect the LAN IP address (first non-loopback IPv4 address on an Up interface).
$LanIp = (Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -ne "127.0.0.1" -and $_.PrefixOrigin -ne "WellKnown" } |
    Sort-Object -Property InterfaceIndex |
    Select-Object -First 1).IPAddress

if (-not $LanIp) {
    Write-Host "ERROR: Could not detect a LAN IP address. Are you connected to Wi-Fi?" -ForegroundColor Red
    exit 1
}

Write-Host "Enabling LAN testing mode for IP: $LanIp" -ForegroundColor Green

# 1. Add firewall rules.
netsh advfirewall firewall add rule name="Happie Dev Server" dir=in action=allow protocol=tcp localport=5195 | Out-Null
netsh advfirewall firewall add rule name="Happie Dev API" dir=in action=allow protocol=tcp localport=7071 | Out-Null
Write-Host "  [OK] Firewall rules added (ports 5195, 7071)"

# 2. Update launchSettings.json to bind on all interfaces.
$launchSettings = Get-Content "Happie.Web\Properties\launchSettings.json" -Raw
$launchSettings = $launchSettings -replace '"applicationUrl": "http://localhost:5195"', '"applicationUrl": "http://0.0.0.0:5195"'
Set-Content "Happie.Web\Properties\launchSettings.json" $launchSettings
Write-Host "  [OK] launchSettings.json -> 0.0.0.0:5195"

# 3. Update appsettings.json (production fallback, used when env is not Development).
$appSettings = Get-Content "Happie.Web\wwwroot\appsettings.json" -Raw
$appSettings = $appSettings -replace '"ApiBaseUrl": "https://happie-func\.azurewebsites\.net/api/"', "`"ApiBaseUrl`": `"http://${LanIp}:7071/api/`""
Set-Content "Happie.Web\wwwroot\appsettings.json" $appSettings
Write-Host "  [OK] appsettings.json -> http://${LanIp}:7071/api/"

# 4. Update appsettings.Development.json.
$appSettingsDev = Get-Content "Happie.Web\wwwroot\appsettings.Development.json" -Raw
$appSettingsDev = $appSettingsDev -replace '"ApiBaseUrl": "http://localhost:7071/api/"', "`"ApiBaseUrl`": `"http://${LanIp}:7071/api/`""
Set-Content "Happie.Web\wwwroot\appsettings.Development.json" $appSettingsDev
Write-Host "  [OK] appsettings.Development.json -> http://${LanIp}:7071/api/"

# 5. Update CORS in local.settings.json.
$localSettings = Get-Content "Happie.Api\local.settings.json" -Raw
$localSettings = $localSettings -replace '"CORS": "http://localhost:5195"', "`"CORS`": `"http://localhost:5195,http://${LanIp}:5195`""
Set-Content "Happie.Api\local.settings.json" $localSettings
Write-Host "  [OK] local.settings.json CORS -> added http://${LanIp}:5195"

Write-Host ""
Write-Host "LAN testing mode enabled!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart the API:      cd Happie.Api; func start --host 0.0.0.0"
Write-Host "  2. Restart the frontend: dotnet run --project Happie.Web --launch-profile http"
Write-Host "  3. On iPhone, visit:     http://${LanIp}:5195" -ForegroundColor Cyan
