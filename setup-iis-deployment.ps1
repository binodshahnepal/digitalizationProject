# ==============================================================================
# BPKMCH Hospital Records AI - Automated Local IIS Deployment Script
# Domain: http://www.digital.com
# ==============================================================================

param (
    [string]$DomainName = "www.digital.com",
    [int]$Port = 80
)

# 1. Self-Elevation Check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host " Requesting Administrator Privileges for IIS Setup...      " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Cyan
    Start-Process powershell.exe -Verb RunAs -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"")
    exit
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " BPKMCH Hospital Records AI - IIS Automated Deployment     " -ForegroundColor Green
Write-Host " Target Domain: http://$DomainName                        " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

$ProjectRoot = "E:\Ibrahim Sir\hospital-digitization-ai"
$IisRoot = "C:\inetpub\wwwroot\digital-hospital"
$FrontendPublishDir = "$IisRoot\frontend"
$ApiPublishDir = "$IisRoot\api"

# 2. Configure Windows Hosts File for www.digital.com
Write-Host "`n[Step 1/7] Configuring Local Domain Resolution in hosts file..." -ForegroundColor Cyan
$hostsPath = "C:\Windows\System32\drivers\etc\hosts"
$hostsContent = Get-Content $hostsPath -Raw

if ($hostsContent -notmatch "(?m)^\s*127\.0\.0\.1\s+$DomainName") {
    $entry = "`r`n127.0.0.1 $DomainName`r`n127.0.0.1 digital.com`r`n"
    Add-Content -Path $hostsPath -Value $entry -Force
    Write-Host "  -> Added '127.0.0.1 $DomainName' to hosts file." -ForegroundColor Green
} else {
    Write-Host "  -> Domain '$DomainName' already configured in hosts file." -ForegroundColor DarkGray
}

# 3. Enable IIS Windows Features
Write-Host "`n[Step 2/7] Verifying and Enabling IIS Features..." -ForegroundColor Cyan
$iisFeatures = @(
    "IIS-WebServerRole",
    "IIS-WebServer",
    "IIS-CommonHttpFeatures",
    "IIS-StaticContent",
    "IIS-DefaultDocument",
    "IIS-HttpErrors",
    "IIS-HttpRedirect",
    "IIS-ApplicationDevelopment",
    "IIS-NetFxExtensibility45",
    "IIS-ASPNET45",
    "IIS-WebSockets"
)

foreach ($feature in $iisFeatures) {
    $status = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue
    if ($null -eq $status -or $status.State -ne 'Enabled') {
        Write-Host "  -> Enabling $feature..." -ForegroundColor Yellow
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart -All -ErrorAction SilentlyContinue | Out-Null
    }
}
Write-Host "  -> IIS Web Server Role is enabled and active." -ForegroundColor Green

# 4. Install IIS URL Rewrite and ASP.NET Core Hosting Bundle via winget
Write-Host "`n[Step 3/7] Verifying IIS URL Rewrite & ASP.NET Core Hosting Bundle..." -ForegroundColor Cyan

# URL Rewrite
$urlRewriteDll = "C:\Windows\System32\inetsrv\rewrite.dll"
if (-not (Test-Path $urlRewriteDll)) {
    Write-Host "  -> Installing Microsoft.IIS.URLRewrite via winget..." -ForegroundColor Yellow
    winget install Microsoft.IIS.URLRewrite --silent --accept-package-agreements --accept-source-agreements --force
} else {
    Write-Host "  -> IIS URL Rewrite Module is installed." -ForegroundColor Green
}

# ASP.NET Core 10.0 Hosting Bundle
$aspnetCoreDll = "C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if (-not (Test-Path $aspnetCoreDll)) {
    Write-Host "  -> Installing Microsoft.DotNet.HostingBundle.10 via winget..." -ForegroundColor Yellow
    winget install Microsoft.DotNet.HostingBundle.10 --silent --accept-package-agreements --accept-source-agreements --force
} else {
    Write-Host "  -> ASP.NET Core Hosting Bundle is installed." -ForegroundColor Green
}

# 5. Build and Publish Frontend & Backend
Write-Host "`n[Step 4/7] Staging and Publishing Production Bundles..." -ForegroundColor Cyan

# Stop IIS to prevent file lock on running DLLs
Write-Host "  -> Stopping IIS to release file locks..." -ForegroundColor Yellow
& iisreset /stop

# Create deployment directories
New-Item -ItemType Directory -Path $FrontendPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $ApiPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path "$ApiPublishDir\logs" -Force | Out-Null

# Copy Frontend
Write-Host "  -> Staging Angular Frontend..." -ForegroundColor Yellow
Copy-Item -Path "$ProjectRoot\publish\frontend\*" -Destination $FrontendPublishDir -Recurse -Force

# Copy Backend
Write-Host "  -> Staging .NET Web API..." -ForegroundColor Yellow
Copy-Item -Path "$ProjectRoot\publish\api\*" -Destination $ApiPublishDir -Recurse -Force

# 6. Configure NTFS Security Permissions
Write-Host "`n[Step 5/7] Applying NTFS Permissions for IIS..." -ForegroundColor Cyan
$acl = Get-Acl $IisRoot
$permission1 = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$permission2 = New-Object System.Security.AccessControl.FileSystemAccessRule("IUSR", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($permission1)
$acl.AddAccessRule($permission2)
Set-Acl $IisRoot $acl
Write-Host "  -> Permissions configured for IIS_IUSRS and IUSR." -ForegroundColor Green

# 7. Configure IIS Web Site and Applications
Write-Host "`n[Step 6/7] Configuring IIS Sites, AppPools and Bindings..." -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction SilentlyContinue

# Disable conflicting port 80 binding on Default Web Site if present
if (Test-Path "IIS:\Sites\Default Web Site") {
    $defaultSite = Get-Item "IIS:\Sites\Default Web Site"
    $hasPort80 = $defaultSite.bindings.Collection | Where-Object { $_.bindingInformation -eq "*:80:" }
    if ($hasPort80) {
        Write-Host "  -> Rebinding or stopping Default Web Site to release port 80..." -ForegroundColor Yellow
        Stop-WebSite -Name "Default Web Site" -ErrorAction SilentlyContinue
    }
}

# Create Application Pools
$apiPoolName = "DigitalApiPool"
$frontendPoolName = "DigitalFrontendPool"

if (-not (Test-Path "IIS:\AppPools\$apiPoolName")) {
    $pool = New-Item "IIS:\AppPools\$apiPoolName"
    $pool.managedRuntimeVersion = ""
    $pool | Set-Item
} else {
    Set-ItemProperty "IIS:\AppPools\$apiPoolName" -Name "managedRuntimeVersion" -Value ""
}

if (-not (Test-Path "IIS:\AppPools\$frontendPoolName")) {
    $pool = New-Item "IIS:\AppPools\$frontendPoolName"
    $pool.managedRuntimeVersion = ""
    $pool | Set-Item
} else {
    Set-ItemProperty "IIS:\AppPools\$frontendPoolName" -Name "managedRuntimeVersion" -Value ""
}

# Create / Update Web Site for www.digital.com
$siteName = "DigitalHospitalSite"
if (Test-Path "IIS:\Sites\$siteName") {
    Remove-WebSite -Name $siteName
}

New-WebSite -Name $siteName `
    -Port $Port `
    -HostHeader $DomainName `
    -PhysicalPath $FrontendPublishDir `
    -ApplicationPool $frontendPoolName | Out-Null

# Add binding for digital.com without www
New-WebBinding -Name $siteName -Port $Port -HostHeader "digital.com" -ErrorAction SilentlyContinue | Out-Null

# Create /api Sub-Application pointing to .NET API
New-WebApplication -Site $siteName `
    -Name "api" `
    -PhysicalPath $ApiPublishDir `
    -ApplicationPool $apiPoolName | Out-Null

# 8. Restart IIS & Launch
Write-Host "`n[Step 7/7] Starting Services and Launching Site..." -ForegroundColor Cyan
iisreset /restart

Start-Process "http://$DomainName"

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " SUCCESS! Deployment completed successfully!               " -ForegroundColor Green
Write-Host " You can now access your application at:                  " -ForegroundColor White
Write-Host "   -> http://$DomainName/                                " -ForegroundColor Yellow
Write-Host "   -> http://$DomainName/api/swagger (API Documentation) " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
