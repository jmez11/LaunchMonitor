param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot "artifacts/LaunchMonitor-$Runtime"
$archive = Join-Path $repoRoot "artifacts/LaunchMonitor-$Runtime.zip"

dotnet publish (Join-Path $repoRoot 'src/LaunchMonitor/LaunchMonitor.csproj') `
    --configuration Release --runtime $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    --output $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Copy-Item (Join-Path $repoRoot 'README.md') $output
Copy-Item (Join-Path $repoRoot 'LICENSE') $output
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$archive.sha256" -Value "$hash  LaunchMonitor-$Runtime.zip" -Encoding ascii
Write-Host "Portable package: $archive"
