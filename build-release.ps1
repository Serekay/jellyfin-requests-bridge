# Build script for Jellyfin Requests Bridge
# Creates ZIP package and updates manifest.json with checksum + version info

param(
    [string]$Version = "1.0.0",
    [string]$TargetAbi = "10.11.0.0"
)

$ErrorActionPreference = "Stop"

$FullVersion = if ($Version -match '^\d+\.\d+\.\d+\.\d+$') { $Version } else { "$Version.0" }
$Tfm = "net9.0"

Write-Host "=== Jellyfin Plugin Release Builder ===" -ForegroundColor Cyan
Write-Host "Version: $FullVersion"
Write-Host "Target ABI: $TargetAbi"

# Paths
$ProjectDir  = "$PSScriptRoot\Jellyfin.Plugin.RequestsBridge"
$OutputDir   = "$PSScriptRoot\dist_new"
$ReleaseDir  = "$OutputDir\v$Version"
$ZipName     = "Jellyfin.Plugin.RequestsBridge.zip"
$ZipPath     = "$ReleaseDir\$ZipName"
$CsprojPath  = "$ProjectDir\Jellyfin.Plugin.RequestsBridge.csproj"
$DllPath     = "$ProjectDir\bin\Release\$Tfm\Jellyfin.Plugin.RequestsBridge.dll"
$MetaPath    = "$ProjectDir\meta.json"
$ThumbPath   = "$ProjectDir\Web\thumb.png"
$TempDir     = "$OutputDir\temp"

# 0) Update version in csproj
Write-Host "`n[0/5] Updating csproj version..." -ForegroundColor Yellow
$CsprojContent = Get-Content $CsprojPath -Raw
$CsprojContent = $CsprojContent -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$FullVersion</AssemblyVersion>"
$CsprojContent = $CsprojContent -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$FullVersion</FileVersion>"
$CsprojContent = $CsprojContent -replace '<Version>.*?</Version>', "<Version>$FullVersion</Version>"
Set-Content $CsprojPath $CsprojContent -NoNewline

# 1) Build
Write-Host "`n[1/5] Building Release..." -ForegroundColor Yellow
dotnet build "$ProjectDir" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 2) Create release folder
Write-Host "`n[2/4] Creating release folder..." -ForegroundColor Yellow
if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null

# 3) Create ZIP (DLL + meta.json + thumb.png)
Write-Host "`n[3/4] Creating ZIP package..." -ForegroundColor Yellow
if (-not (Test-Path $DllPath))  { throw "DLL not found: $DllPath" }
if (-not (Test-Path $MetaPath)) { throw "meta.json not found: $MetaPath" }
if (-not (Test-Path $ThumbPath)) { throw "thumb.png not found: $ThumbPath" }

if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

Copy-Item $DllPath   $TempDir
Copy-Item $ThumbPath $TempDir

# meta.json with current version
$Meta = Get-Content $MetaPath -Raw | ConvertFrom-Json
$Meta.version = $FullVersion
$Meta | ConvertTo-Json -Depth 10 | Set-Content "$TempDir\meta.json" -Encoding UTF8

Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipPath -Force
Remove-Item $TempDir -Recurse -Force

# 4) MD5 checksum
Write-Host "`n[4/4] Calculating checksum..." -ForegroundColor Yellow
$Hash = (Get-FileHash $ZipPath -Algorithm MD5).Hash.ToLower()
Write-Host "Checksum: $Hash" -ForegroundColor Green

# 5) Update manifest.json
Write-Host "`nUpdating manifest.json..." -ForegroundColor Yellow
$ManifestPath = "$PSScriptRoot\manifest.json"
$Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$Timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$ExistingVersion = $Manifest[0].versions | Where-Object { $_.version -eq $FullVersion -and $_.targetAbi -eq $TargetAbi }
if ($ExistingVersion) {
    $ExistingVersion.checksum = $Hash
    $ExistingVersion.timestamp = $Timestamp
} else {
    $NewVersion = @{
        version   = $FullVersion
        changelog = "Update"
        targetAbi = $TargetAbi
        sourceUrl = "https://github.com/Serekay/jellyfin-requests-bridge/releases/download/v$Version/$ZipName"
        checksum  = $Hash
        timestamp = $Timestamp
    }
    $Manifest[0].versions = @($NewVersion) + $Manifest[0].versions
}

$Manifest | ConvertTo-Json -Depth 10 -Compress | Set-Content $ManifestPath -Encoding UTF8

Write-Host "`n=== Release Complete ===" -ForegroundColor Green
Write-Host "ZIP: $ZipPath"
Write-Host "Checksum: $Hash"
Write-Host "`nNaechste Schritte:"
Write-Host "1. Commit & Push zu GitHub"
Write-Host "2. GitHub Release erstellen mit Tag 'v$Version'"
Write-Host "3. ZIP-Datei zum Release hochladen"
Write-Host "4. manifest.json URL in Jellyfin hinzufuegen:"
Write-Host "   https://raw.githubusercontent.com/Serekay/jellyfin-requests-bridge/main/manifest.json" -ForegroundColor Cyan
